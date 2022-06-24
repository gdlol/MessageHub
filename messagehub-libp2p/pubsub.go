package main

import (
	"context"
	"sync"

	"github.com/libp2p/go-libp2p-core/peer"
	"github.com/libp2p/go-libp2p-kad-dht/dual"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/p2p/discovery/routing"
)

type MemberStore struct {
	mutex   sync.RWMutex
	members map[string]map[string]any
}

func NewMemberStore() MemberStore {
	return MemberStore{
		members: make(map[string]map[string]any),
	}
}

func (store *MemberStore) getMembers(topic string) []string {
	store.mutex.RLock()
	defer store.mutex.RUnlock()
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
	store.mutex.Lock()
	defer store.mutex.Unlock()
	delete(store.members, topic)
}

func (store *MemberStore) addMember(topic string, peerID string) {
	store.mutex.Lock()
	defer store.mutex.Unlock()
	ids, ok := store.members[topic]
	if !ok {
		ids = make(map[string]any)
		store.members[topic] = ids
	}
	ids[peerID] = nil
}

func (store *MemberStore) removeMember(topic string, peerID string) {
	store.mutex.Lock()
	defer store.mutex.Unlock()
	if ids, ok := store.members[topic]; ok {
		delete(ids, peerID)
	}
}

// Filter white list of peers for each topic.
func (store *MemberStore) filterPeer(pid peer.ID, topic string) bool {
	store.mutex.RLock()
	defer store.mutex.RUnlock()
	pidString := peer.Encode(pid)
	if peerIDs, ok := store.members[topic]; ok {
		_, ok := peerIDs[pidString]
		return ok
	}
	return false
}

func createPubSub(ctx context.Context, dualDHT *dual.DHT, store *MemberStore) (*pubsub.PubSub, error) {
	discovery := routing.NewRoutingDiscovery(dualDHT)
	options := []pubsub.Option{
		pubsub.WithDiscovery(discovery),
		pubsub.WithPeerFilter(store.filterPeer),
	}
	gossipSub, err := pubsub.NewGossipSub(ctx, dualDHT.WAN.Host(), options...)
	return gossipSub, err
}
