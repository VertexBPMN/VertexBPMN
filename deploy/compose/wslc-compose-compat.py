"""Project-local WSLC 2.9 JSON/NDJSON adapter; never modifies installed tools."""
import argparse
import json
import re
import sys


def parse_wslc_json(output, command):
    output = output.strip()
    if not output:
        return []
    try:
        parsed = json.loads(output)
    except json.JSONDecodeError:
        parsed = [json.loads(line) for line in output.splitlines() if line.strip()]
    if command in ("images", "list") and isinstance(parsed, dict):
        parsed = [parsed]
    if command == "list":
        for item in parsed:
            item.setdefault("Id", item.get("ID"))
            item.setdefault("Name", item.get("Names"))
            if isinstance(item.get("Ports"), str):
                ports = []
                for display in item["Ports"].split(","):
                    display = display.strip()
                    if not display or "->" not in display:
                        continue  # unpublished container port, not a host binding
                    match = re.fullmatch(r"(.+):(\d+)->(\d+)/(tcp|udp)", display)
                    if not match:
                        raise ValueError("Unrecognized WSLC port binding format")
                    address, host, container, protocol = match.groups()
                    ports.append({"BindingAddress": address, "HostPort": int(host),
                                  "ContainerPort": int(container), "Protocol": 6 if protocol == "tcp" else 17})
                item["Ports"] = ports
    return parsed


def main():
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--compose-source")
    options, arguments = parser.parse_known_args()
    if options.compose_source:
        sys.path.insert(0, options.compose_source)
    try:
        from wslc_compose import engine
        from wslc_compose.cli import main as compose_main
    except ImportError:
        raise SystemExit("Install the wslc_compose Python package or specify -ComposeSource pointing to its src directory.")

    def capture_json(args):
        return parse_wslc_json(engine.run(args, capture=True).stdout, args[0])

    engine.capture_json = capture_json
    return compose_main(arguments)


if __name__ == "__main__":
    raise SystemExit(main())
