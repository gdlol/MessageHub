package main

// #include <stdlib.h>
import "C"
import (
	"context"
	"sync"
)

type ObjectStore struct {
	mutex    sync.RWMutex
	objectId int64
	values   map[int64]any
}

func NewObjectStore() ObjectStore {
	return ObjectStore{
		objectId: 0,
		values:   make(map[int64]any),
	}
}

var objectStore ObjectStore = NewObjectStore()

func (store *ObjectStore) Store(value any) int64 {
	store.mutex.Lock()
	defer store.mutex.Unlock()
	store.objectId += 1
	if store.objectId == 0 {
		panic("Object Id limit exceeded.")
	}
	store.values[store.objectId] = value
	return store.objectId
}

func (store *ObjectStore) Load(id int64) (value any, ok bool) {
	store.mutex.RLock()
	defer store.mutex.RUnlock()
	v, ok := store.values[id]
	return v, ok
}

func (store *ObjectStore) Delete(id int64) {
	store.mutex.Lock()
	defer store.mutex.Unlock()
	delete(store.values, id)
}

type IntPtr *C.void
type ObjectHandle int64
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
	id := objectStore.Store(v)
	return ObjectHandle(id)
}

func loadValue(id ObjectHandle) any {
	value, ok := objectStore.Load(int64(id))
	if ok {
		return value
	}
	return nil
}

func deleteValue(id ObjectHandle) {
	objectStore.Delete(int64(id))
}
