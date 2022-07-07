package main

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"os"

	"github.com/libp2p/go-libp2p-core/host"
	p2phttp "github.com/libp2p/go-libp2p-http"
)

func download(ctx context.Context, host host.Host, peerID, url, filePath string) error {
	transport := &http.Transport{}
	transport.RegisterProtocol("libp2p", p2phttp.NewTransport(host))
	client := &http.Client{Transport: transport}
	url = fmt.Sprintf("libp2p://%s%s", peerID, url)
	request, err := http.NewRequest(http.MethodGet, url, nil)
	if err != nil {
		return err
	}
	request = request.WithContext(ctx)

	response, err := client.Do(request)
	if err != nil {
		return err
	}
	defer response.Body.Close()

	if response.StatusCode != http.StatusOK {
		return fmt.Errorf("status: %v", response.Status)
	}

	file, err := os.Create(filePath)
	if err != nil {
		return err
	}
	defer file.Close()

	_, err = io.Copy(file, response.Body)
	if err != nil {
		return err
	}
	return nil
}
