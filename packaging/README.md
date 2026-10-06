# Package manager manifests

## winget

`winget/manifests/` mirrors the layout of
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs). To publish a version, copy
`manifests/a/AhdAljadeed/KnockKnock/<version>/` into a fork of winget-pkgs and open a pull
request. Check it locally first:

```powershell
winget validate --manifest packaging\winget\manifests\a\AhdAljadeed\KnockKnock\1.0.0
```

For later releases, [wingetcreate](https://github.com/microsoft/winget-create) can open the
pull request for you:

```powershell
wingetcreate update AhdAljadeed.KnockKnock --version 1.1.0 `
  --urls https://github.com/Ahd-Aljadeed/Knock-Knock/releases/download/v1.1.0/KnockKnock.exe --submit
```

## Scoop

`scoop/knock-knock.json` installs straight from this repository:

```powershell
scoop install https://raw.githubusercontent.com/Ahd-Aljadeed/Knock-Knock/main/packaging/scoop/knock-knock.json
```

It uses `checkver` and `autoupdate`, so it can also be submitted to the
[Scoop Extras](https://github.com/ScoopInstaller/Extras) bucket.
