#!/bin/bash

# Format whitespace (indentation, line endings, etc.)
dotnet format whitespace

# Fix code style issues
dotnet format style

# Run third-party analyzers and apply fixes
dotnet format analyzers
