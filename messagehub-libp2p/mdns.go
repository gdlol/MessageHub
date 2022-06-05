package main

import (
	"context"

	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/peer"
	"github.com/libp2p/go-libp2p/p2p/discovery/mdns"
)

type mdnsService struct {
	service mdns.Service
	ctx     context.Context
	cancel  context.CancelFunc
}

type mdnsNotifee struct {
	host host.Host
	ctx  context.Context
}

func (notifee mdnsNotifee) HandlePeerFound(addrInfo peer.AddrInfo) {
	err := notifee.host.Connect(notifee.ctx, addrInfo)
	if err != nil {
		notifee.host.ConnManager().Protect(addrInfo.ID, "mdns")
	}
}

func newMdnsService(ctx context.Context, host host.Host, serviceName string) mdnsService {
	ctx, cancel := context.WithCancel(ctx)
	notifee := mdnsNotifee{
		host: host,
		ctx:  ctx,
	}
	service := mdns.NewMdnsService(host, serviceName, notifee)
	return mdnsService{
		service: service,
		ctx:     ctx,
		cancel:  cancel,
	}
}
