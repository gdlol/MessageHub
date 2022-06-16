FROM golang:1.18
WORKDIR /root/build/Data/WebView2
CMD wget -O setup.exe https://go.microsoft.com/fwlink/p/?LinkId=2124703
