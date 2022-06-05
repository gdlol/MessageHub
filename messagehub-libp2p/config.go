package main

type HostConfig struct {
	StaticRelays         *[]string
	DataPath             string
	PrivateNetworkSecret *string
}

type DHTConfig struct {
	BootstrapPeers *[]string
}
