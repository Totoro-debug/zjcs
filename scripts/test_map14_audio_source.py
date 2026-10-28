"""Regression coverage for shared/bank-embedded original Wwise media resolution."""
import struct
import unittest
from unittest.mock import patch
from map14_audio_source import AudioSource, sha

def chunk(tag,raw):return tag+struct.pack("<I",len(raw))+raw

def source(media_id=398253717,offset=3,size=8,payload=b"xxxRIFFtest"):
    bank=chunk(b"BKHD",b"header")+chunk(b"DIDX",struct.pack("<III",media_id,offset,size))+chunk(b"DATA",payload)
    prefix=b"pck-prefix"
    obj=AudioSource.__new__(AudioSource);obj.packages={"test_pck.rawfile":(prefix+bank,{})};obj.media_search_complete=True
    return obj,[(27,1,len(bank),len(prefix),0)]

class OfficialBankMediaTests(unittest.TestCase):
    def test_embedded_media_uses_actual_package_bank_data_offset(self):
        obj,banks=source()
        with patch("map14_audio_source.parse_pck",return_value=(banks,[])):
            raw,records=obj.media(398253717)
        self.assertEqual(raw,b"RIFFtest")
        self.assertEqual(records[0]["sha256"],sha(b"RIFFtest"))
        self.assertEqual(obj.packages["test_pck.rawfile"][0][records[0]["offset"]:][:8],raw)
        self.assertEqual(records[0]["bankId"],27)
    def test_truncated_bank_data_is_rejected(self):
        obj,banks=source(size=9)
        with patch("map14_audio_source.parse_pck",return_value=(banks,[])):
            with self.assertRaisesRegex(ValueError,"Truncated original bank media"):obj.media(398253717)
    def test_same_global_media_id_with_conflicting_package_bytes_is_rejected(self):
        obj=AudioSource.__new__(AudioSource);obj.media_search_complete=True
        obj.packages={"a_pck.rawfile":(b"RIFFgood",{}),"b_pck.rawfile":(b"RIFFbad!",{})}
        with patch("map14_audio_source.parse_pck",return_value=([],[(99,1,8,0,0)])):
            with self.assertRaisesRegex(ValueError,"Conflicting original Wwise media"):obj.media(99)
    def test_byte_identical_shared_media_keeps_both_source_receipts(self):
        obj=AudioSource.__new__(AudioSource);obj.media_search_complete=True
        obj.packages={"a_pck.rawfile":(b"RIFFgood",{}),"b_pck.rawfile":(b"RIFFgood",{})}
        with patch("map14_audio_source.parse_pck",return_value=([],[(99,1,8,0,0)])):
            raw,records=obj.media(99)
        self.assertEqual(raw,b"RIFFgood");self.assertEqual(len(records),2)
    def test_streaming_prefetch_uses_full_original_media_and_retains_fragment_receipt(self):
        obj=AudioSource.__new__(AudioSource);obj.media_search_complete=True
        obj.packages={"prefetch_pck.rawfile":(b"RIFF",{}),"stream_pck.rawfile":(b"RIFFcomplete",{})}
        def entries(raw):return [],[(99,1,len(raw),0,0)]
        with patch("map14_audio_source.parse_pck",side_effect=entries):raw,records=obj.media(99)
        self.assertEqual(raw,b"RIFFcomplete")
        self.assertEqual([r["prefetchFragment"] for r in records],[True,False])
        self.assertEqual(records[0]["sha256"],sha(b"RIFF"))

if __name__=="__main__":unittest.main()
