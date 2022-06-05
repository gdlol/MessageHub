package main

// #include <stdlib.h>
import "C"
import (
	"context"
	"sync"

	"github.com/google/uuid"
)

var objectStore sync.Map

type IntPtr *C.void
type ObjectHandle *C.char
type StringHandle *C.char
type ContextHandle = ObjectHandle
type HostHandle = ObjectHandle
type ProxyHandle = ObjectHandle
type MdnsServiceHandle = ObjectHandle
type DHTHandle = ObjectHandle
type DiscoveryHandle = ObjectHandle
type PeerChanHandle = ObjectHandle
type MemberStoreHandle = ObjectHandle
type PubSubHandle = ObjectHandle
type TopicHandle = ObjectHandle
type SubscriptionHandle = ObjectHandle

type cancellableContext struct {
	ctx    context.Context
	cancel context.CancelFunc
}

func saveValue(v any) ObjectHandle {
	id := uuid.NewString()
	objectStore.Store(id, v)
	return C.CString(id)
}

func loadValue(id ObjectHandle) any {
	s := C.GoString(id)
	value, ok := objectStore.Load(s)
	if ok {
		return value
	}
	return nil
}

func deleteValue(id ObjectHandle) {
	s := C.GoString(id)
	objectStore.Delete(s)
}
