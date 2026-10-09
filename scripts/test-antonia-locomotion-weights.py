"""Regression checks for continuous anatomical support across region boundaries."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("locomotion_weights",
    Path(__file__).with_name("repair-antonia-locomotion-weights.py"))
repair = importlib.util.module_from_spec(spec)
spec.loader.exec_module(repair)


def body_with(vertices):
    positions = {"Neck": (0, 0, 1510), "Head": (0, 0, 1570)}
    for side, x in (("Left", -90), ("Right", 90)):
        positions[side + "Hip"] = (x, 0, 960)
        positions[side + "Knee"] = (x, 0, 524)
        positions[side + "Ankle"] = (x, 0, 80)
    joints = [{"kind": name, "globalBind": dict(zip(("m41", "m42", "m43"), position))}
              for name, position in positions.items()]
    indices = {joint["kind"]: index for index, joint in enumerate(joints)}
    return dict(skeleton=dict(joints=joints), surface=dict(
        vertices=[dict(region=region, position=dict(zip(("x", "y", "z"), position)))
                  for region, position, weights in vertices],
        skinWeights=[dict(weights=[dict(jointIndex=indices[name], weight=value)
                                  for name, value in weights.items()])
                     for region, position, weights in vertices]))


class WeightSupportTests(unittest.TestCase):
    def test_neck_jaw_region_boundary_has_no_weight_seam(self):
        weights = dict(Neck=.4, Head=.6)
        body = body_with([("Head", (20, 80, 1564), weights),
                          ("Neck", (20, 80, 1564), weights),
                          ("Neck", (20, 0, 1490), weights)])
        repair.prepare(body, 200)
        rows = body["surface"]["skinWeights"]
        self.assertEqual(rows[0], rows[1])
        self.assertEqual(rows[0]["weights"], [dict(jointIndex=1, weight=1)])
        self.assertGreater(rows[2]["weights"][0]["weight"], 0)

    def test_bilateral_knee_support_preserves_other_channels_and_geometry(self):
        body = body_with([("LeftShin", (-90, -70, 510), dict(LeftHip=.45, LeftKnee=.45, Head=.1)),
                          ("RightShin", (90, -70, 510), dict(RightHip=.45, RightKnee=.45, Head=.1))])
        original = copy.deepcopy(body)
        repair.prepare(body, 200)
        self.assertEqual(body["surface"]["vertices"], original["surface"]["vertices"])
        self.assertEqual(body["skeleton"], original["skeleton"])
        left, right = body["surface"]["skinWeights"]
        self.assertEqual([item["weight"] for item in left["weights"]],
                         [item["weight"] for item in right["weights"]])
        for row in (left, right):
            self.assertAlmostEqual(sum(item["weight"] for item in row["weights"]), 1)
            self.assertAlmostEqual(row["weights"][0]["weight"], .1)

    def test_support_outside_knee_neighborhood_is_unchanged(self):
        body = body_with([("LeftFoot", (-90, 90, 50), dict(LeftKnee=.3, LeftAnkle=.7))])
        original = copy.deepcopy(body)
        repair.prepare(body, 200)
        self.assertEqual(body, original)


if __name__ == "__main__":
    unittest.main()
