import unittest

import torch

from identify import rank_observation, registration_decision


class RegistrationDecisionTest(unittest.TestCase):
    def test_requires_animal_evidence_and_a_distinct_species(self):
        cases = [
            (0.75, 0.70, 0.62, "accepted"),
            (0.75, 0.70, 0.72, "no_animal"),
            (0.68, 0.61, 0.56, "uncertain"),
            (0.75, 0.72, 0.62, "uncertain"),
            (0.75, 0.78, 0.62, "uncertain"),
            (float("nan"), 0.70, 0.62, "uncertain"),
        ]
        for best, runner_up, background, expected in cases:
            with self.subTest(scores=(best, runner_up, background)):
                self.assertEqual(registration_decision(best, runner_up, background), expected)

    def test_complementary_views_resolve_different_close_competitors(self):
        views = torch.tensor([[.76, .74, .57], [.76, .57, .74]])
        other = torch.tensor([[.66], [.66]])
        background = torch.tensor([[.61], [.61]])
        for i in range(2):
            self.assertEqual(rank_observation(views[i:i+1], other[i:i+1], background[i:i+1])[0], "uncertain")
        decision, _, top = rank_observation(views, other, background)
        self.assertEqual((decision, top[0].item()), ("accepted", 0))

    def test_one_conflicting_or_background_view_blocks_the_observation(self):
        views = torch.tensor([[.80, .60, .58]] * 4 + [[.63, .76, .57]])
        other = torch.full((5, 1), .60)
        background = torch.full((5, 1), .59)
        self.assertEqual(rank_observation(views, other, background)[0], "uncertain")
        background[-1] = .75
        self.assertEqual(rank_observation(views, other, background)[0], "no_animal")


if __name__ == "__main__":
    unittest.main()
