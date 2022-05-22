package main

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"net/netip"
	"time"

	"github.com/libp2p/go-libp2p"
	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/peer"
	"github.com/libp2p/go-libp2p-core/pnet"
	gostream "github.com/libp2p/go-libp2p-gostream"
	p2phttp "github.com/libp2p/go-libp2p-http"
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
	Signatures            map[string]map[string]string `json:"signatures"`
}

func createHost(config HostConfig) (host.Host, error) {
	options := []libp2p.Option{
		libp2p.EnableNATService(),
		libp2p.AutoNATServiceRateLimit(60, 3, time.Minute),
		libp2p.EnableHolePunching(),
	}
	if !config.AdvertisePrivateAddresses {
		options = append(options, libp2p.AddrsFactory(func(m []multiaddr.Multiaddr) []multiaddr.Multiaddr {
			return multiaddr.FilterAddrs(m, manet.IsPublicAddr)
		}))
	}
	if len(config.StaticRelays) > 0 {
		relayAddrInfos := make([]peer.AddrInfo, 0)
		for _, s := range config.StaticRelays {
			relayAddrInfo, err := peer.AddrInfoFromString(s)
			if err != nil {
				return nil, fmt.Errorf("Error parsing static relay address: %w", err)
			}
			relayAddrInfos = append(relayAddrInfos, *relayAddrInfo)
		}
		options = append(options, libp2p.StaticRelays(relayAddrInfos))
	}
	if len(config.PrivateNetworkSecret) > 0 {
		info := []byte("messagehub-libp2p private network")
		reader := hkdf.New(sha256.New, []byte(config.PrivateNetworkSecret), nil, info)
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
	return host, nil
}

func sendRequest(ctx context.Context, host host.Host, peerID peer.ID, signedRequest SignedRequest) (int, []byte, error) {
	senderSignatures, ok := signedRequest.Signatures[signedRequest.Origin]
	if !ok {
		return 0, nil, fmt.Errorf("Sender signature not found.")
	}

	transport := &http.Transport{}
	transport.RegisterProtocol("libp2p", p2phttp.NewTransport(host))
	client := &http.Client{Transport: transport}
	var request *http.Request
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

	request.Header.Add("Matrix-Host", signedRequest.Destination)
	request.Header.Add("Matrix-Timestamp", fmt.Sprint(signedRequest.OriginServerTimestamp))
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
