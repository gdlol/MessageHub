package main

type HostConfig struct {
	AdvertisePrivateAddresses bool
	StaticRelays              []string
	DataPath                  string
	PrivateNetworkSecret      string
}

type DHTConfig struct {
	BootstrapPeers         []string
	FilterPrivateAddresses bool
}
