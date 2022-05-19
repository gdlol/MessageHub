package main

import (
	"context"

	dht "github.com/libp2p/go-libp2p-kad-dht"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/p2p/discovery/routing"
)

func createPubSub(ctx context.Context, ipfsDHT *dht.IpfsDHT) (*pubsub.PubSub, error) {
	discovery := routing.NewRoutingDiscovery(ipfsDHT)
	gossipSub, err := pubsub.NewGossipSub(ctx, ipfsDHT.Host(), pubsub.WithDiscovery(discovery))
	return gossipSub, err
}
