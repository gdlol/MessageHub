package main

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"math/rand"
	"net"
	"net/http"
	"net/netip"
	"os"
	"path/filepath"
	"sync"
	"time"

	"github.com/ipfs/go-datastore"
	leveldb "github.com/ipfs/go-ds-leveldb"
	"github.com/libp2p/go-libp2p"
	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/network"
	"github.com/libp2p/go-libp2p-core/peer"
	"github.com/libp2p/go-libp2p-core/peerstore"
	"github.com/libp2p/go-libp2p-core/pnet"
	gostream "github.com/libp2p/go-libp2p-gostream"
	p2phttp "github.com/libp2p/go-libp2p-http"
	"github.com/libp2p/go-libp2p-peerstore/pstoreds"
	"github.com/libp2p/go-libp2p/p2p/host/autorelay"
	"github.com/libp2p/go-tcp-transport"
	"github.com/multiformats/go-multiaddr"
	manet "github.com/multiformats/go-multiaddr/net"
	"golang.org/x/crypto/hkdf"
)

type SignedRequest struct {
	Method                string                       `json:"method"`
	Uri                   string                       `json:"uri"`
	Origin                string                       `json:"origin"`
	OriginServerTimestamp int64                        `json:"origin_server_ts"`
	Destination           string                       `json:"destination"`
	Content               map[string]any               `json:"content"`
	ServerKeys            any                          `json:"server_keys"`
	Signatures            map[string]map[string]string `json:"signatures"`
}

type HostNode struct {
	ds   datastore.Batching
	ps   peerstore.Peerstore
	host host.Host
}

func createHost(config HostConfig) (*HostNode, error) {
	// Create peer store
	dataPath := filepath.Join(config.DataPath, "libp2p")
	err := os.MkdirAll(dataPath, os.ModePerm)
	if err != nil {
		return nil, fmt.Errorf("Error creating dataPath: %w", err)
	}
	dbPath := filepath.Join(dataPath, "datastore.db")
	ds, err := leveldb.NewDatastore(dbPath, nil)
	if err != nil {
		log.Fatalln(fmt.Errorf("Error creating DataStore: %w", err))
	}
	ps, err := pstoreds.NewPeerstore(context.Background(), ds, pstoreds.DefaultOpts())
	if err != nil {
		log.Fatalln(fmt.Errorf("Error creating PeerStore: %w", err))
		ds.Close()
	}
	success := false
	defer func() {
		if !success {
			ps.Close()
			ds.Close()
		}
	}()

	peerSource := make(chan peer.AddrInfo)
	options := []libp2p.Option{
		libp2p.Peerstore(ps),
		libp2p.EnableNATService(),
		libp2p.AutoNATServiceRateLimit(60, 3, time.Minute),
		libp2p.EnableHolePunching(),
		libp2p.EnableRelayService(),
		libp2p.EnableAutoRelay(autorelay.WithPeerSource(peerSource)),
	}
	if config.StaticRelays != nil {
		relayAddrInfos := make([]peer.AddrInfo, 0)
		for _, s := range *config.StaticRelays {
			relayAddrInfo, err := peer.AddrInfoFromString(s)
			if err != nil {
				return nil, fmt.Errorf("Error parsing static relay address: %w", err)
			}
			relayAddrInfos = append(relayAddrInfos, *relayAddrInfo)
		}
		options = append(options, libp2p.StaticRelays(relayAddrInfos))
	}
	if config.PrivateNetworkSecret != nil {
		info := []byte("libp2p")
		reader := hkdf.New(sha256.New, []byte(*config.PrivateNetworkSecret), nil, info)
		psk := make([]byte, 32)
		_, err := reader.Read(psk)
		if err != nil {
			return nil, fmt.Errorf("Error configuring private network: %w", err)
		}
		options = append(options, libp2p.PrivateNetwork(pnet.PSK(psk)))
		options = append(options, libp2p.Transport(tcp.NewTCPTransport))
		options = append(options, libp2p.ListenAddrStrings("/ip4/0.0.0.0/tcp/0"))
		options = append(options, libp2p.ListenAddrStrings("/ip6/::/tcp/0"))
	}
	host, err := libp2p.New(options...)
	if err != nil {
		return nil, err
	}
	peerNotifiee := &network.NotifyBundle{
		ConnectedF: func(n network.Network, c network.Conn) {
			peerSource <- peer.AddrInfo{
				ID:    c.RemotePeer(),
				Addrs: []multiaddr.Multiaddr{c.RemoteMultiaddr()},
			}
		},
	}
	host.Network().Notify(peerNotifiee)
	success = true
	hostNode := &HostNode{
		ds:   ds,
		ps:   ps,
		host: host,
	}
	return hostNode, nil
}

func connectToSavedPeers(ctx context.Context, host host.Host) int {
	// Load saved AddrInfos from PeerStore.
	maxCandidateCount := 20
	savedAddrInfos := make([]peer.AddrInfo, 0, maxCandidateCount)
	ps := host.Peerstore()
	savedPeerIDs := ps.Peers()
	if len(savedPeerIDs) > 0 {
		for _, i := range rand.Perm(len(savedPeerIDs)) {
			peerID := savedPeerIDs[i]
			if peerID == host.ID() {
				continue
			}
			addrInfo := ps.PeerInfo(peerID)
			publicAddrs := multiaddr.FilterAddrs(addrInfo.Addrs, manet.IsPublicAddr)
			if len(publicAddrs) == 0 {
				continue
			}
			publicAddrInfo := peer.AddrInfo{
				ID:    addrInfo.ID,
				Addrs: publicAddrs,
			}
			savedAddrInfos = append(savedAddrInfos, publicAddrInfo)
			if len(savedAddrInfos) >= maxCandidateCount {
				break
			}
		}
	}

	if len(savedAddrInfos) > 0 {
		connectPeer := func(wg *sync.WaitGroup, addrInfo peer.AddrInfo) {
			defer wg.Done()
			timeoutCtx, timeoutCancel := context.WithTimeout(ctx, time.Second*10)
			defer timeoutCancel()

			host.Connect(timeoutCtx, addrInfo)
		}

		var wg sync.WaitGroup
		for _, addrInfo := range savedAddrInfos {
			select {
			case <-ctx.Done():
				break
			default:
				wg.Add(1)
				go connectPeer(&wg, addrInfo)
			}
		}
		wg.Wait()
	}

	return len(host.Network().Peers())
}

func sendRequest(ctx context.Context, host host.Host, peerID peer.ID, signedRequest SignedRequest) (int, []byte, error) {
	senderSignatures, ok := signedRequest.Signatures[signedRequest.Origin]
	if !ok {
		return 0, nil, fmt.Errorf("Sender signature not found.")
	}

	transport := &http.Transport{}
	transport.RegisterProtocol("libp2p", p2phttp.NewTransport(host))
	client := &http.Client{Transport: transport}
	url := fmt.Sprintf("libp2p://%s%s", peerID, signedRequest.Uri)
	var body io.Reader
	if signedRequest.Content != nil {
		requestBody, err := json.Marshal(signedRequest.Content)
		if err != nil {
			return 0, nil, err
		}
		body = bytes.NewReader(requestBody)
	}
	request, err := http.NewRequest(signedRequest.Method, url, body)
	if err != nil {
		return 0, nil, err
	}
	serverKeysJson, err := json.Marshal(signedRequest.ServerKeys)
	if err != nil {
		return 0, nil, err
	}
	encodedServerKeys := hex.EncodeToString(serverKeysJson)

	request.Header.Add("Matrix-Host", signedRequest.Destination)
	request.Header.Add("Matrix-Timestamp", fmt.Sprint(signedRequest.OriginServerTimestamp))
	request.Header.Add("Matrix-ServerKeys", encodedServerKeys)
	request.Header.Set("Content-Type", "application/json")
	for key, signature := range senderSignatures {
		args := []any{signedRequest.Origin, key, signature}
		header := fmt.Sprintf("X-Matrix origin=%s,key=\"%s\",sig=\"%s\"", args...)
		request.Header.Add("Authorization", header)
	}
	request = request.WithContext(ctx)
	response, err := client.Do(request)
	if err != nil {
		return 0, nil, err
	}
	defer response.Body.Close()
	responseBody, err := io.ReadAll(response.Body)
	return response.StatusCode, responseBody, err
}

func proxyRequests(host host.Host, proxy string) (func() error, error) {
	addrPort, err := netip.ParseAddrPort(proxy)
	if err != nil {
		return nil, err
	}
	tcpAddr := net.TCPAddrFromAddrPort(addrPort)
	listener, err := gostream.Listen(host, p2phttp.DefaultP2PProtocol)
	if err != nil {
		return nil, err
	}
	go func() {
		for {
			conn, err := listener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer conn.Close()
				tcpConn, err := net.DialTCP("tcp", nil, tcpAddr)
				if err != nil {
					log.Println(err)
					return
				}
				defer tcpConn.Close()
				errChan := make(chan error)
				go func() {
					_, err = io.Copy(conn, tcpConn)
					tcpConn.CloseWrite()
					errChan <- err
				}()
				go func() {
					_, err = io.Copy(tcpConn, conn)
					errChan <- err
				}()
				_ = <-errChan
				_ = <-errChan
			}()
		}
	}()
	return listener.Close, nil
}
