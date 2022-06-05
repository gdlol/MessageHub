package main

import (
	"github.com/libp2p/go-libp2p-core/control"
	"github.com/libp2p/go-libp2p-core/network"
	"github.com/libp2p/go-libp2p-core/peer"
	ma "github.com/multiformats/go-multiaddr"
	manet "github.com/multiformats/go-multiaddr/net"
)

type privateAddressGater struct{}

func (gater *privateAddressGater) InterceptPeerDial(p peer.ID) (allow bool) {
	return true
}

func (gater *privateAddressGater) InterceptAddrDial(_ peer.ID, addr ma.Multiaddr) (allow bool) {
	return manet.IsPrivateAddr(addr)
}

func (gater *privateAddressGater) InterceptAccept(addrs network.ConnMultiaddrs) (allow bool) {
	return manet.IsPrivateAddr(addrs.RemoteMultiaddr())
}

func (gater *privateAddressGater) InterceptSecured(_ network.Direction, _ peer.ID, addrs network.ConnMultiaddrs) (allow bool) {
	return manet.IsPrivateAddr(addrs.RemoteMultiaddr())
}

func (gater *privateAddressGater) InterceptUpgraded(conn network.Conn) (allow bool, reason control.DisconnectReason) {
	return manet.IsPrivateAddr(conn.RemoteMultiaddr()), 0
}
