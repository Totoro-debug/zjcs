import sys
import unittest
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_home_sky_timeline import pad_sprite_image, serializable_track, validate_track_targets


class HomeSkyTimelineTests(unittest.TestCase):
    def test_trimmed_sprite_is_restored_to_original_rect_and_pivot_space(self):
        source = Image.new('RGBA', (3, 2), (255, 0, 0, 255))
        restored = pad_sprite_image(source, (8, 7), {'x': 2, 'y': 1})
        self.assertEqual(restored.size, (8, 7))
        self.assertEqual(restored.getpixel((2, 7 - 1 - 2)), (255, 0, 0, 255))
        self.assertEqual(restored.getpixel((0, 0)), (0, 0, 0, 0))

    def test_solar_track_preserves_hermite_tangents(self):
        track = serializable_track({
            'path': '/Day_Common/Sun/Scale', 'property': 'position.x',
            'keys': [[0.0, -32.1, 0.0, 1.75], [24.0, -0.56, -5.2, 0.0]],
        })
        self.assertFalse(track['stepped'])
        self.assertEqual(track['keys'][0]['outTangent'], 1.75)
        self.assertEqual(track['keys'][1]['inTangent'], -5.2)

    def test_active_tracks_are_serialized_as_stepped_values(self):
        track = serializable_track({
            'path': '/Day_Common/Sun/Scale', 'property': 'm_IsActive',
            'keys': [[0.0, 0.0, None, None], [8.0, 1.0, None, None]],
        })
        self.assertTrue(track['stepped'])
        self.assertEqual([(key['time'], key['value']) for key in track['keys']], [(0.0, 0.0), (8.0, 1.0)])

    def test_timeline_bindings_match_existing_renderer_and_light_components(self):
        nodes = [{'path': '/Day_Common/Sun'}, {'path': '/Day_Common/Sun/SunLight'}]
        renderers = [{'path': '/Day_Common/Sun'}]
        lights = [{'path': '/Day_Common/Sun/SunLight'}]
        tracks = [
            {'path': '/Day_Common/Sun', 'property': 'position.x'},
            {'path': '/Day_Common/Sun', 'property': 'm_Color.a'},
            {'path': '/Day_Common/Sun/SunLight', 'property': 'm_Intensity'},
            {'path': '/Day_Common/Sun/SunLight', 'property': 'm_ShadowIntensity'},
        ]
        validate_track_targets(tracks, nodes, renderers, lights)

    def test_timeline_rejects_duplicate_or_unhandled_bindings(self):
        nodes = [{'path': '/Day_Common/Sun'}]
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            validate_track_targets([
                {'path': '/Day_Common/Sun', 'property': 'position.x'},
                {'path': '/Day_Common/Sun', 'property': 'position.x'},
            ], nodes, [], [])
        with self.assertRaisesRegex(ValueError, 'Unsupported'):
            validate_track_targets([
                {'path': '/Day_Common/Sun', 'property': 'm_Unexpected'},
            ], nodes, [], [])


if __name__ == '__main__':
    unittest.main()
