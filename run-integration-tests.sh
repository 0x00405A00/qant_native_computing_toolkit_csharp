#!/usr/bin/env bash
# Runs all tests against the CPU-backend library in ./native
cd "$(dirname "$0")"
QANT_NATIVE_LIB_PATH="$PWD/native/libqant_native_computing_toolkit.so" dotnet test
