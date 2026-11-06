"""Keep original particle vertex-stream bytes through native prefab conversion."""
import unittest
import yaml
from prepare_current_partner_native import UnityYamlLoader
from prepare_partner_detail_actor_native import PartnerDetailActorBinding


class PartnerNativeYamlTests(unittest.TestCase):
    def test_embedded_mesh_keeps_empty_external_stream_path(self):
        # A null path emitted by the converter becomes the literal filename
        # "null" in Unity, so its embedded vertex bytes are not loaded.
        binding = object.__new__(PartnerDetailActorBinding)
        source = {'m_StreamData': {'serializedVersion': 2, 'offset': 0,
                                  'size': 0, 'path': None}}
        converted = binding.replace(source)
        self.assertEqual(converted['m_StreamData']['path'], '')
        self.assertIsNone(source['m_StreamData']['path'])
        nonempty = {'m_StreamData': {'offset': 0, 'size': 24, 'path': 'mesh.resS'}}
        self.assertEqual(binding.replace(nonempty), nonempty)

    def test_original_particle_vertex_streams_keep_leading_zeroes(self):
        # NpcDetailLiveItem_8416's actual renderer streams are [0, 1, 3, 4].
        # YAML 1.1 interprets the exported scalar as octal 4292 without the adapter.
        for field in ('m_VertexStreams', 'm_TrailVertexStreams'):
            with self.subTest(field=field):
                document = yaml.load(field + ': 00010304\n', Loader=UnityYamlLoader)
                reloaded = yaml.load(yaml.safe_dump(document), Loader=UnityYamlLoader)
                self.assertEqual(reloaded[field], '00010304')
                self.assertEqual(list(bytes.fromhex(reloaded[field])), [0, 1, 3, 4])


if __name__ == '__main__':
    unittest.main()
