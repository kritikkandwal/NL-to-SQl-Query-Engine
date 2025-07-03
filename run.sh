#!/bin/bash
echo "Starting NLP to SQL API..."
echo
echo "Installing dependencies..."
dotnet restore
echo
echo "Building application..."
dotnet build
echo
echo "Starting server on http://localhost:5000"
echo "Press Ctrl+C to stop the server"
echo
dotnet run