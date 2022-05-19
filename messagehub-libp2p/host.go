package main

import (
	"crypto/sha256"
	"fmt"
	"time"

	"github.com/libp2p/go-libp2p"
	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/peer"
	"github.com/libp2p/go-libp2p-core/pnet"
	"github.com/libp2p/go-tcp-transport"
	"github.com/multiformats/go-multiaddr"
	manet "github.com/multiformats/go-multiaddr/net"
	"golang.org/x/crypto/hkdf"
)

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
	}
	host, err := libp2p.New(options...)
	if err != nil {
		return nil, err
	}
	return host, nil
}
