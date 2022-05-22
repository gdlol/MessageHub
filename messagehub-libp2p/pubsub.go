package main

import (
	"context"

	"github.com/libp2p/go-libp2p-core/peer"
	dht "github.com/libp2p/go-libp2p-kad-dht"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/p2p/discovery/routing"
)

type MemberStore struct {
	members map[string]map[string]any
}

func NewMemberStore() MemberStore {
	return MemberStore{
		members: make(map[string]map[string]any),
	}
}

func (store *MemberStore) getMembers(topic string) []string {
	if ids, ok := store.members[topic]; ok {
		result := make([]string, 0, len(ids))
		for id := range ids {
			result = append(result, id)
		}
		return result
	} else {
		return make([]string, 0)
	}
}

func (store *MemberStore) clearMembers(topic string) {
	delete(store.members, topic)
}

func (store *MemberStore) addMember(topic string, peerID string) {
	ids, ok := store.members[topic]
	if !ok {
		ids = make(map[string]any)
		store.members[topic] = ids
	}
	ids[peerID] = nil
}

func (store *MemberStore) removeMember(topic string, peerID string) {
	if ids, ok := store.members[topic]; ok {
		delete(ids, peerID)
	}
}

// Filter while list of peers for each topic.
func (store *MemberStore) filterPeer(pid peer.ID, topic string) bool {
	if peerIDs, ok := store.members[topic]; ok {
		_, ok := peerIDs[pid.String()]
		return ok
	}
	return false
}

func createPubSub(ctx context.Context, ipfsDHT *dht.IpfsDHT, store *MemberStore) (*pubsub.PubSub, error) {
	discovery := routing.NewRoutingDiscovery(ipfsDHT)
	options := []pubsub.Option{
		pubsub.WithDiscovery(discovery),
		pubsub.WithPeerFilter(store.filterPeer),
	}
	gossipSub, err := pubsub.NewGossipSub(ctx, ipfsDHT.Host(), options...)
	return gossipSub, err
}
