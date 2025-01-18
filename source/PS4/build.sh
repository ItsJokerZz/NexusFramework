clear

echo "-----------------------------------------------------------------------------"
echo "Building DAEMON..."
echo "-----------------------------------------------------------------------------"

cd DAEMON
make master
cd ..

BUILD=$(cat DAEMON/build/.number)
VERSION=$(grep -oP '^VERSION\s*:=\s*\K[^\s]*' DAEMON/Makefile)

echo
echo "-----------------------------------------------------------------------------"
echo "Building PAYLOAD..."
echo "-----------------------------------------------------------------------------"

cd PAYLOAD
make
cp OrbisControl.bin ../../PC/ConsoleManager/bin/Debug
cp OrbisControl.bin ../../PC/ConsoleManager/bin/Release
cd ..

echo 
echo "-----------------------------------------------------------------------------"
echo "Building PLUGIN..."
echo "-----------------------------------------------------------------------------"

cd PLUGIN
make
cd ..

echo "-----------------------------------------------------------------------------"

echo
echo "------------- OrbisControl v$VERSION"b$BUILD ON $(date +"%m/%d/%Y @ %I:%M:%S %p") "-------------"
echo "-----------------------------------------------------------------------------"
