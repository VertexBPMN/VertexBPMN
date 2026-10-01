import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location("compat", pathlib.Path(__file__).with_name("wslc-compose-compat.py"))
compat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compat)


class CompatibilityTests(unittest.TestCase):
    def test_empty(self):
        self.assertEqual([], compat.parse_wslc_json("", "list"))

    def test_old_array(self):
        self.assertEqual([{"Id": "1", "Name": "api"}], compat.parse_wslc_json('[{"Id":"1","Name":"api"}]', "list"))

    def test_ndjson(self):
        result = compat.parse_wslc_json('{"ID":"1","Names":"api"}\n{"ID":"2","Names":"studio"}', "list")
        self.assertEqual(["1", "2"], [item["Id"] for item in result])
        self.assertEqual(["api", "studio"], [item["Name"] for item in result])

    def test_single_image(self):
        self.assertEqual([{"ID": "image"}], compat.parse_wslc_json('{"ID":"image"}', "images"))

    def test_display_ports_become_typed_bindings(self):
        result = compat.parse_wslc_json('{"Ports":"127.0.0.1:51870->51870/tcp, [::1]:8080->80/udp"}', "list")[0]
        self.assertEqual(2, len(result["Ports"]))
        self.assertEqual("127.0.0.1", result["Ports"][0]["BindingAddress"])
        self.assertEqual(51870, result["Ports"][0]["HostPort"])
        self.assertEqual(17, result["Ports"][1]["Protocol"])

    def test_unpublished_ports_are_not_reported_as_host_bindings(self):
        self.assertEqual([], compat.parse_wslc_json('{"Ports":"8080/tcp"}', "list")[0]["Ports"])

    def test_single_inspect_stays_object(self):
        self.assertEqual({"State": {"Running": True}}, compat.parse_wslc_json('{"State":{"Running":true}}', "inspect"))

    def test_invalid_json_fails_instead_of_hiding_engine_errors(self):
        with self.assertRaises(ValueError):
            compat.parse_wslc_json("Access denied", "list")


if __name__ == "__main__":
    unittest.main()
