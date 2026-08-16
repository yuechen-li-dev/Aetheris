package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
)

type artifact struct { Path string `json:"path"` }
type response struct {
	Success bool `json:"success"`
	Artifacts []artifact `json:"artifacts"`
}

func main() {
	host, requestPath, outputDir := os.Args[1], os.Args[2], os.Args[3]
	request, err := os.ReadFile(requestPath)
	if err != nil { panic(err) }
	command := exec.Command(host, "invoke", "Standard.SheetMetal.ElectronicsEnclosure", "--request", "-", "--out", outputDir)
	command.Stdin = bytes.NewReader(request)
	var stdout bytes.Buffer
	command.Stdout = &stdout
	command.Stderr = os.Stderr
	if err = command.Run(); err != nil { panic(err) }
	var result response
	if err = json.Unmarshal(stdout.Bytes(), &result); err != nil { panic(err) }
	if !result.Success { panic("Forge Host invocation failed") }
	for _, item := range result.Artifacts {
		if _, err = os.Stat(filepath.Join(outputDir, item.Path)); err != nil { panic(err) }
	}
	fmt.Print(stdout.String())
}
