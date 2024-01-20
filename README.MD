# AssettoServer Template Parser
This Program will scan a template folder and "generate" a preset collection.

## Before reading!

Depending on the platform the Template Parser executable will be `ASTemplateParser.exe` or `ASTemplateParser`.
In the following context it will just be called `ASTemplateParser`

Depending on the platform the AssettoServer executable will be `AssettoServer.exe` or `AssettoServer`.
In the following context it will just be called `AssettoServer`


## Workflow

### Folder Structure
```
Server Base Folder
┃   ┃   AssettoServer(.exe)
┃   ┃   ...
┃
┣━━━templates
┃   ┃   template_cfg.json
┃   ┃
┃   ┣━━━shutoko
┃   ┃   ┃   server_cfg.ini
┃   ┃   ┃   extra_cfg.yml
┃   ┃   ┃   ...
┃   ┃
┃   ┗━━━shutoko
┃       ┃   server_cfg.ini
┃       ┃   extra_cfg.yml
┃       ┃   ...
┃   
┣━━━presets
┃   ┣━━━shutoko
┃   ┃   ┃   server_cfg.ini
┃   ┃   ┃   extra_cfg.yml
┃   ┃   ┃   ...
┃   ┃
┃   ┗━━━shutoko
┃       ┃   server_cfg.ini
┃       ┃   extra_cfg.yml
┃       ┃   ...
┃
┣━━━cfg/
┣━━━log/
┗━━━...
```

### Preperations
#### template_cfg.json
This config is basically a key-value-pair (string-string)
```json
{
    "ADMIN_PASSWORD": "TEST1234,.",
    "TCP_PORT": "9777",
    "UDP_PORT": "9777",
    "HTTP_PORT": "8777",
    "SERVER_NAME": "This is some shitty testing stoof",
    "TICKRATE": "60",
    "PLACEHOLDER": "funny noises"
}
```
#### Config files
In every file that is located in the `template/` subdirectories this program will scan for a specific pattern. `welcome.txt` would also be processed.

Pattern: `[$PLACEHOLDER]` with `PLACEHOLDER` being the key that will be matched within the `template_cfg.json` or environment variable if enabled.

The `server_cfg.ini` could look like this:
```ini
...
ADMIN_PASSWORD = [$ADMIN_PASSWORD]
UDP_PORT = [$UDP_PORT]
TCP_PORT = [$TCP_PORT]
HTTP_PORT = [$HTTP_PORT]
NAME = [$SERVER_NAME]
CLIENT_SEND_INTERVAL_HZ = [$TICKRATE]
...
```
Looking into the `presets/` directory you will see that this pattern will be replaced with the provided values.

### Usage
1. Put `ASTemplateParser` into the same folder as `AssettoServer`
2. Start `ASTemplateParser` to generate a new `presets/` directory using the `templates/` directory.
3. Launch `AssettoServer` after closing `ASTemplateParser`


### Available Launch Options

| Full                 | Short | Description                                                                                   |
|----------------------|-------|-----------------------------------------------------------------------------------------------|
| --use-env-vars       | -e    | Use environment variables when key is not found in config file (templates/template_cfg.json). |
| --delete-old-presets | -x    | Should old presets/ be deleted instead of moved for backup.                                   |

