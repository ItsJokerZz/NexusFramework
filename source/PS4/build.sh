clear

echo "-----------------------------------------------------------------------------"
echo "Building DAEMON..."
echo "-----------------------------------------------------------------------------"

cd DAEMON
make master
cd ..
cp DAEMON/OrbisControl.prx ../PC/API/

BUILD=$(cat DAEMON/build/.number)
VERSION=$(grep -oP '^VERSION\s*:=\s*\K[^\s]*' DAEMON/Makefile)

echo
echo "-----------------------------------------------------------------------------"
echo "Building PAYLOAD..."
echo "-----------------------------------------------------------------------------"

cd PAYLOAD
make
cd ..
cp PAYLOAD/OrbisControl.bin ../PC/API/

echo "-----------------------------------------------------------------------------"
echo
echo "------------- OrbisControl v$VERSION"b$BUILD ON $(date +"%m/%d/%Y @ %I:%M:%S %p") "-------------"
echo "-----------------------------------------------------------------------------"
