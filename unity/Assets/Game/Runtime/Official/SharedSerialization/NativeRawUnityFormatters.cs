using MessagePack;
using MessagePack.Formatters;
using UnityEngine;

namespace MessagePack.Unity
{
 // Original GFrameCore AOT bodies 0x1018710c / 0x101871d4.
 public sealed class Vector3Formatter : IMessagePackFormatter<Vector3>
 {
  public int Serialize(ref byte[] bytes,int offset,Vector3 value,IFormatterResolver resolver)
  {
   int start=offset;
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.x);
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.y);
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.z);
   return offset-start;
  }
  public Vector3 Deserialize(byte[] bytes,int offset,IFormatterResolver resolver,out int readSize)
  {
   int start=offset;
   float x=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   float y=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   float z=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   readSize=offset-start;return new Vector3(x,y,z);
  }
 }
 // Original GFrameCore AOT bodies 0x101876fc / 0x101877dc.
 public sealed class QuaternionFormatter : IMessagePackFormatter<Quaternion>
 {
  public int Serialize(ref byte[] bytes,int offset,Quaternion value,IFormatterResolver resolver)
  {
   int start=offset;
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.x);
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.y);
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.z);
   offset+=MessagePackBinary.WriteSingle(ref bytes,offset,value.w);
   return offset-start;
  }
  public Quaternion Deserialize(byte[] bytes,int offset,IFormatterResolver resolver,out int readSize)
  {
   int start=offset;
   float x=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   float y=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   float z=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   float w=MessagePackBinary.ReadSingle(bytes,offset,out readSize);offset+=readSize;
   readSize=offset-start;return new Quaternion(x,y,z,w);
  }
 }
}
