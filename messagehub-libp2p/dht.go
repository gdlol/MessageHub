package main

import (
	"context"
	"fmt"

	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/peer"
	dht "github.com/libp2p/go-libp2p-kad-dht"
)

func createDHT(ctx context.Context, host host.Host, config DHTConfig) (*dht.IpfsDHT, error) {
	options := make([]dht.Option, 0)
	if len(config.BootstrapPeers) > 0 {
		bootstrapPeers := make([]peer.AddrInfo, len(config.BootstrapPeers))
		for _, s := range config.BootstrapPeers {
			addrInfo, err := peer.AddrInfoFromString(s)
			if err != nil {
				return nil, fmt.Errorf("Error parsing bootstrap peeers address: %w", err)
			}
			bootstrapPeers = append(bootstrapPeers, *addrInfo)
		}
		options = append(options, dht.BootstrapPeers(bootstrapPeers...))
	}
	if config.FilterPrivateAddresses {
		filterOptions := []dht.Option{
			dht.QueryFilter(dht.PublicQueryFilter),
			dht.RoutingTableFilter(dht.PublicRoutingTableFilter),
		}
		options = append(options, filterOptions...)
	}
	ipfsDHT, err := dht.New(ctx, host, options...)
	return ipfsDHT, err
}
