package main

type HostConfig struct {
	Port                 int
	StaticRelays         *[]string
	DataPath             string
	PrivateNetworkSecret *string
}

type DHTConfig struct {
	BootstrapPeers *[]string
}
