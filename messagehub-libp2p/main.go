package main

// #include <stdlib.h>
import "C"
import (
	"context"
	"encoding/json"
	"fmt"

	"github.com/libp2p/go-libp2p-core/host"
	dht "github.com/libp2p/go-libp2p-kad-dht"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
)

//export Test
func Test() StringHandle {
	return C.CString("Hello from go.")
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
	id := host.ID()
	return C.CString(fmt.Sprint(id))
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

//export CreatePubSub
func CreatePubSub(ctxHandle ContextHandle, dhtHandle DHTHandle, pubsubHandle *PubSubHandle) StringHandle {
	*pubsubHandle = nil
	ctx := loadValue(ctxHandle).(*cancellableContext).ctx
	ipfsDHT := loadValue(dhtHandle).(*dht.IpfsDHT)
	gossipSub, err := createPubSub(ctx, ipfsDHT)
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

func main() {}
