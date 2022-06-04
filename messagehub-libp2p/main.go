package main

// #include <stdlib.h>
import "C"
import (
	"context"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"unsafe"

	"github.com/ipfs/go-cid"
	"github.com/libp2p/go-libp2p-core/host"
	"github.com/libp2p/go-libp2p-core/peer"
	dht "github.com/libp2p/go-libp2p-kad-dht"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/p2p/discovery/routing"
	mc "github.com/multiformats/go-multicodec"
	mh "github.com/multiformats/go-multihash"
)

//export Alloc
func Alloc(length int32) IntPtr {
	size := C.size_t(length)
	return (IntPtr)(C.malloc(size))
}

//export Free
func Free(ptr IntPtr) {
	C.free(unsafe.Pointer(ptr))
}

//export Release
func Release(id ObjectHandle) {
	deleteValue(id)
}

//export CreateContext
func CreateContext() ContextHandle {
	ctx, cancel := context.WithCancel(context.Background())
	result := cancellableContext{
		ctx:    ctx,
		cancel: cancel,
	}
	return saveValue(&result)
}

//export CancelContext
func CancelContext(handle ContextHandle) {
	ctx := loadValue(handle).(*cancellableContext)
	ctx.cancel()
}

//export CreateHost
func CreateHost(configJSON StringHandle, handle *HostHandle) StringHandle {
	*handle = nil
	jsonString := C.GoString(configJSON)
	var config HostConfig
	err := json.Unmarshal([]byte(jsonString), &config)
	if err != nil {
		return C.CString(fmt.Sprint("Error parsing JSON config: %w", err))
	}
	host, err := createHost(config)
	if err != nil {
		return C.CString(err.Error())
	}
	*handle = saveValue(host)
	return nil
}

//export CloseHost
func CloseHost(handle HostHandle) StringHandle {
	host := loadValue(handle).(host.Host)
	err := host.Close()
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export GetHostID
func GetHostID(handle HostHandle) StringHandle {
	host := loadValue(handle).(host.Host)
	id := peer.Encode(host.ID())
	return C.CString(id)
}

//export GetHostAddressInfo
func GetHostAddressInfo(hostHandle HostHandle, resultJSON *StringHandle) StringHandle {
	*resultJSON = nil
	host := loadValue(hostHandle).(host.Host)
	addrInfo := peer.AddrInfo{
		ID:    host.ID(),
		Addrs: host.Addrs(),
	}
	result, err := addrInfo.MarshalJSON()
	if err != nil {
		return C.CString(err.Error())
	}
	*resultJSON = C.CString(string(result))
	return nil
}

//export GetIDFromAddressInfo
func GetIDFromAddressInfo(addrInfo StringHandle, peerID *StringHandle) StringHandle {
	*peerID = nil
	var peerAddrInfo peer.AddrInfo
	err := peerAddrInfo.UnmarshalJSON([]byte(C.GoString(addrInfo)))
	if err != nil {
		return C.CString(err.Error())
	}
	result, err := peerAddrInfo.ID.MarshalText()
	if err != nil {
		return C.CString(err.Error())
	}
	*peerID = C.CString(string(result))
	return nil
}

//export ConnectHost
func ConnectHost(ctxHandle ContextHandle, hostHandle HostHandle, addrInfo StringHandle) StringHandle {
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	host := loadValue(hostHandle).(host.Host)
	var peerAddrInfo peer.AddrInfo
	err := peerAddrInfo.UnmarshalJSON([]byte(C.GoString(addrInfo)))
	if err != nil {
		return C.CString(err.Error())
	}
	err = host.Connect(ctx, peerAddrInfo)
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export SendRequest
func SendRequest(ctxHandle ContextHandle, hostHandle HostHandle, peerID StringHandle, signedRequestJSON StringHandle, responseStatus *int, responseBody *StringHandle) StringHandle {
	*responseStatus = 0
	*responseBody = nil
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	host := loadValue(hostHandle).(host.Host)
	p2pPeerID, err := peer.Decode(C.GoString(peerID))
	if err != nil {
		return C.CString(err.Error())
	}
	jsonString := C.GoString(signedRequestJSON)
	var signedRequest SignedRequest
	err = json.Unmarshal([]byte(jsonString), &signedRequest)
	if err != nil {
		return C.CString(err.Error())
	}
	status, body, err := sendRequest(ctx, host, p2pPeerID, signedRequest)
	if err != nil {
		return C.CString(err.Error())
	}
	*responseStatus = status
	*responseBody = C.CString(string(body))
	return nil
}

//export StartProxyRequests
func StartProxyRequests(hostHandle HostHandle, proxy StringHandle, result *ProxyHandle) StringHandle {
	*result = nil
	host := loadValue(hostHandle).(host.Host)
	closeProxy, err := proxyRequests(host, C.GoString(proxy))
	if err != nil {
		return C.CString(err.Error())
	}
	*result = saveValue(closeProxy)
	return nil
}

//export StopProxyRequests
func StopProxyRequests(proxyHandle ProxyHandle) StringHandle {
	closeProxy := loadValue(proxyHandle).(func() error)
	err := closeProxy()
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export CreateMdnsService
func CreateMdnsService(hostHandle HostHandle, serviceName StringHandle) MdnsServiceHandle {
	host := loadValue(hostHandle).(host.Host)
	service := newMdnsService(context.Background(), host, C.GoString(serviceName))
	return saveValue(service)
}

//export StartMdnsService
func StartMdnsService(mdnsServiceHandle MdnsServiceHandle) StringHandle {
	service := loadValue(mdnsServiceHandle).(mdnsService)
	err := service.service.Start()
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export StopMdnsService
func StopMdnsService(mdnsServiceHandle MdnsServiceHandle) StringHandle {
	service := loadValue(mdnsServiceHandle).(mdnsService)
	service.cancel()
	err := service.service.Close()
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export CreateDHT
func CreateDHT(ctxHandle ContextHandle, hostHandle HostHandle, configJSON StringHandle, dhtHandle *DHTHandle) StringHandle {
	*dhtHandle = nil
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	host := loadValue(hostHandle).(host.Host)
	jsonString := C.GoString(configJSON)
	var config DHTConfig
	err := json.Unmarshal([]byte(jsonString), &config)
	if err != nil {
		return C.CString(fmt.Sprint("Error parsing JSON config: %w", err))
	}
	ipfsDHT, err := createDHT(ctx, host, config)
	if err != nil {
		return C.CString(err.Error())
	}
	*dhtHandle = saveValue(ipfsDHT)
	return nil
}

//export CloseDHT
func CloseDHT(handle DHTHandle) StringHandle {
	ipfsDHT := loadValue(handle).(*dht.IpfsDHT)
	err := ipfsDHT.Close()
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export BootstrapDHT
func BootstrapDHT(ctxHandle ContextHandle, dhtHandle DHTHandle) StringHandle {
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	ipfsDHT := loadValue(dhtHandle).(*dht.IpfsDHT)
	err := ipfsDHT.Bootstrap(ctx)
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export FindPeer
func FindPeer(ctxHandle ContextHandle, dhtHandle DHTHandle, peerID StringHandle, resultJSON *StringHandle) StringHandle {
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	ipfsDHT := loadValue(dhtHandle).(*dht.IpfsDHT)
	p2pPeerID, err := peer.Decode(C.GoString(peerID))
	if err != nil {
		return C.CString(err.Error())
	}
	addrInfo, err := ipfsDHT.FindPeer(ctx, p2pPeerID)
	if err != nil {
		return C.CString(err.Error())
	}
	result, err := addrInfo.MarshalJSON()
	if err != nil {
		return C.CString(err.Error())
	}
	*resultJSON = C.CString(string(result))
	return nil
}

//export CreateDiscovery
func CreateDiscovery(dhtHandle DHTHandle) DiscoveryHandle {
	ipfsDHT := loadValue(dhtHandle).(*dht.IpfsDHT)
	discovery := routing.NewRoutingDiscovery(ipfsDHT)
	return saveValue(discovery)
}

//export Advertise
func Advertise(ctxHandle ContextHandle, discoveryHandle DiscoveryHandle, topic StringHandle) StringHandle {
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	discovery := loadValue(discoveryHandle).(*routing.RoutingDiscovery)
	_, err := discovery.Advertise(ctx, C.GoString(topic))
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export FindPeers
func FindPeers(ctxHandle ContextHandle, discoveryHandle DiscoveryHandle, topic StringHandle, resultJSON *StringHandle) StringHandle {
	*resultJSON = nil
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	discovery := loadValue(discoveryHandle).(*routing.RoutingDiscovery)
	peers, err := discovery.FindPeers(ctx, C.GoString(topic))
	if err != nil {
		return C.CString(err.Error())
	}
	result := make(map[string]string, 0)
	for addrInfo := range peers {
		addrInfoJson, err := addrInfo.MarshalJSON()
		if err != nil {
			return C.CString(err.Error())
		}
		result[peer.Encode(addrInfo.ID)] = string(addrInfoJson)
	}
	json, err := json.Marshal(result)
	if err != nil {
		return C.CString(err.Error())
	}
	*resultJSON = C.CString(string(json))
	return nil
}

//export CreateMemberStore
func CreateMemberStore() MemberStoreHandle {
	store := NewMemberStore()
	return saveValue(&store)
}

//export GetMembers
func GetMembers(memberStoreHandle MemberStoreHandle, topic StringHandle, resultJSON *StringHandle) StringHandle {
	*resultJSON = nil
	store := loadValue(memberStoreHandle).(*MemberStore)
	result := store.getMembers(C.GoString(topic))
	json, err := json.Marshal(result)
	if err != nil {
		return C.CString(err.Error())
	}
	*resultJSON = C.CString(string(json))
	return nil
}

//export ClearMembers
func ClearMembers(memberStoreHandle MemberStoreHandle, topic StringHandle) {
	store := loadValue(memberStoreHandle).(*MemberStore)
	store.clearMembers(C.GoString(topic))
}

//export AddMember
func AddMember(memberStoreHandle MemberStoreHandle, topic StringHandle, peerID StringHandle) {
	store := loadValue(memberStoreHandle).(*MemberStore)
	store.addMember(C.GoString(topic), C.GoString(peerID))
}

//export RemoveMember
func RemoveMember(memberStoreHandle MemberStoreHandle, topic StringHandle, peerID StringHandle) {
	store := loadValue(memberStoreHandle).(*MemberStore)
	store.removeMember(C.GoString(topic), C.GoString(peerID))
}

//export CreatePubSub
func CreatePubSub(ctxHandle ContextHandle, dhtHandle DHTHandle, memberStoreHandle MemberStoreHandle, pubsubHandle *PubSubHandle) StringHandle {
	*pubsubHandle = nil
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	ipfsDHT := loadValue(dhtHandle).(*dht.IpfsDHT)
	memberStore := loadValue(memberStoreHandle).(*MemberStore)
	gossipSub, err := createPubSub(ctx, ipfsDHT, memberStore)
	if err != nil {
		return C.CString(err.Error())
	}
	*pubsubHandle = saveValue(gossipSub)
	return nil
}

//export JoinTopic
func JoinTopic(pubsubHandle PubSubHandle, topic StringHandle, topicHandle *TopicHandle) StringHandle {
	*topicHandle = nil
	gossipSub := loadValue(pubsubHandle).(*pubsub.PubSub)
	gossipTopic, err := gossipSub.Join(C.GoString(topic))
	if err != nil {
		return C.CString(err.Error())
	}
	*topicHandle = saveValue(gossipTopic)
	return nil
}

//export CloseTopic
func CloseTopic(topicHandle TopicHandle) StringHandle {
	topic := loadValue(topicHandle).(*pubsub.Topic)
	err := topic.Close()
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export PublishMessage
func PublishMessage(ctxHandle ContextHandle, topicHandle TopicHandle, message StringHandle) StringHandle {
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	topic := loadValue(topicHandle).(*pubsub.Topic)
	err := topic.Publish(ctx, []byte(C.GoString(message)))
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export Subscribe
func Subscribe(topicHandle TopicHandle, subscriptionHandle *SubscriptionHandle) StringHandle {
	*subscriptionHandle = nil
	topic := loadValue(topicHandle).(*pubsub.Topic)
	subscription, err := topic.Subscribe()
	if err != nil {
		return C.CString(err.Error())
	}
	*subscriptionHandle = saveValue(subscription)
	return nil
}

//export CancelSubscription
func CancelSubscription(subscriptionHandle SubscriptionHandle) {
	subscription := loadValue(subscriptionHandle).(*pubsub.Subscription)
	subscription.Cancel()
}

//export GetNextMessage
func GetNextMessage(ctxHandle ContextHandle, subscriptionHandle SubscriptionHandle, senderID *StringHandle, messageJSON *StringHandle) StringHandle {
	*senderID = nil
	*messageJSON = nil
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	subscription := loadValue(subscriptionHandle).(*pubsub.Subscription)
	message, err := subscription.Next(ctx)
	if err != nil {
		return C.CString(err.Error())
	}
	*senderID = C.CString(peer.Encode(message.ReceivedFrom))
	*messageJSON = C.CString(string(message.Data))
	return nil
}

//export DownloadFile
func DownloadFile(ctxHandle ContextHandle, hostHandle HostHandle, peerID StringHandle, url StringHandle, filePath StringHandle) StringHandle {
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	host := loadValue(hostHandle).(host.Host)
	err := download(ctx, host, C.GoString(peerID), C.GoString(url), C.GoString(filePath))
	if err != nil {
		return C.CString(err.Error())
	}
	return nil
}

//export EncodeEd25519PublicKey
func EncodeEd25519PublicKey(hexPublicKey StringHandle, result *StringHandle) StringHandle {
	*result = nil
	publicKey, err := hex.DecodeString(C.GoString(hexPublicKey))
	if err != nil {
		return C.CString(err.Error())
	}
	prefix := cid.Prefix{
		Version:  1,
		Codec:    uint64(mc.Ed25519Pub),
		MhType:   mh.IDENTITY,
		MhLength: -1,
	}
	id, err := prefix.Sum(publicKey)
	if err != nil {
		return C.CString(err.Error())
	}
	*result = C.CString(id.String())
	return nil
}

//export DecodeEd25519PublicKey
func DecodeEd25519PublicKey(s StringHandle, result *StringHandle) StringHandle {
	*result = nil
	id, err := cid.Decode(C.GoString(s))
	if err != nil {
		return C.CString(err.Error())
	}
	hash, err := mh.Decode(id.Hash())
	if err != nil {
		return C.CString(err.Error())
	}
	hexPublicKey := hex.EncodeToString(hash.Digest)
	*result = C.CString(hexPublicKey)
	return nil
}

func main() {}
