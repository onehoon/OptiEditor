# Local Velopack update test

Run the manual **Release** workflow twice to create two consecutive `v0.1.x`
GitHub Releases. Install the first release with its generated `Setup.exe`, then
start OptiEditor and confirm it downloads the later release, restarts, and
launches the newer version.

Run this check on a clean Windows 10 version 1809-or-later machine and on a
supported Windows 11 machine. Confirm the app starts after both the initial
install and the automatic restart. Do not use the test fixtures as package
inputs; they are test-only files.
