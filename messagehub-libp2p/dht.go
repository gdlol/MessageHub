package main

import (
	"context"
	"fmt"

	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/peer"
	dht "github.com/libp2p/go-libp2p-kad-dht"
	"github.com/libp2p/go-libp2p-kad-dht/dual"
)

func createDHT(ctx context.Context, host host.Host, config DHTConfig) (*dual.DHT, error) {
	options := make([]dht.Option, 0)
	if config.BootstrapPeers == nil {
		options = append(options, dht.BootstrapPeers(dht.GetDefaultBootstrapPeerAddrInfos()...))
	} else {
		bootstrapPeers := make([]peer.AddrInfo, len(*config.BootstrapPeers))
		for _, s := range *config.BootstrapPeers {
			addrInfo, err := peer.AddrInfoFromString(s)
			if err != nil {
				return nil, fmt.Errorf("Error parsing bootstrap peers address: %w", err)
			}
			bootstrapPeers = append(bootstrapPeers, *addrInfo)
		}
		options = append(options, dht.BootstrapPeers(bootstrapPeers...))
	}
	dualDHT, err := dual.New(ctx, host, dual.DHTOption(options...))
	return dualDHT, err
}
