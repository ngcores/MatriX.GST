#!/usr/bin/env bash
DEST="/opt/matrixgst"

apt-get update
apt-get install -y wget unzip sysstat iproute2 lsof
apt-get install -y libgstreamer1.0-0 libgstreamer-plugins-base1.0-0 gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-base-apps gstreamer1.0-plugins-ugly gstreamer1.0-libav gstreamer1.0-tools ca-certificates

# Install .NET
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh 
chmod 755 dotnet-install.sh
./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Download zip
mkdir $DEST -p 
cd $DEST
wget https://github.com/ngcores/MatriX.GST/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip

# Create service
echo ""
echo "Install service to /etc/systemd/system/matrixgst.service ..."
touch /etc/systemd/system/matrixgst.service && chmod 664 /etc/systemd/system/matrixgst.service
cat <<EOF > /etc/systemd/system/matrixgst.service
[Unit]
Description=matrixgst
Wants=network.target
After=network.target
[Service]
WorkingDirectory=$DEST
ExecStart=/usr/bin/dotnet MatriX.GST.dll
#ExecReload=/bin/kill -s HUP $MAINPID
#ExecStop=/bin/kill -s QUIT $MAINPID
Restart=always
[Install]
WantedBy=multi-user.target
EOF

# Enable service
systemctl daemon-reload
systemctl enable matrixgst
systemctl start matrixgst

# iptables drop
cat <<EOF > iptables-drop.sh
#!/bin/sh
echo "Stopping firewall and allowing everyone..."
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
iptables -P INPUT ACCEPT
iptables -P FORWARD ACCEPT
iptables -P OUTPUT ACCEPT
EOF

chmod +x iptables-drop.sh

# Note
echo ""
echo "################################################################"
echo ""
echo "Have fun!"
echo ""
echo "Then [re]start it as systemctl [re]start matrixgst"
echo ""
echo "Clear iptables if port 8590 is not available"
echo "bash $DEST/iptables-drop.sh"
echo ""
