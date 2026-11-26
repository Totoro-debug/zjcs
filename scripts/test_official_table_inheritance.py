"""Regression checks for Luban dictionary pools forwarded through base constructors."""
import unittest
from official_tables import Constructors, Reader


class PooledInheritanceTests(unittest.TestCase):
    def parser(self):
        parser = Constructors.__new__(Constructors)
        parser.cache = {}
        parser.sources = {
            'PropCfg': """public abstract class PropCfg {
                public PropCfg(ByteBuf _buf) { Value = _buf.ReadInt(); }
                public PropCfg(ByteBuf _buf, Dictionary<int, long>[] pool) {
                    Factors = pool[_buf.ReadSize()];
                }
            }""",
            'GroupCfg': """public abstract class GroupCfg : PropCfg {
                public GroupCfg(ByteBuf _buf) : base(_buf) { Group = _buf.ReadInt(); }
                public GroupCfg(ByteBuf _buf, Dictionary<int, long>[] pool)
                    : base(_buf, pool) { Group = _buf.ReadInt(); }
            }""",
            'EntityCfg': """public class EntityCfg : GroupCfg {
                public EntityCfg(ByteBuf _buf) : base(_buf) { Entity = _buf.ReadInt(); }
                public EntityCfg(ByteBuf _buf, Dictionary<int, long>[] pool)
                    : base(_buf, pool) { Entity = _buf.ReadInt(); }
            }""",
        }
        return parser

    def test_pool_identity_and_base_read_order(self):
        parser = self.parser()
        pool = [{413: 595}, {413: 605}]
        reader = Reader(bytes([1, 33, 99]))
        row = parser.decode('EntityCfg', reader, pool)
        self.assertIs(row.Factors, pool[1])
        self.assertEqual((row.Group, row.Entity, reader.pos), (33, 99, 3))

    def test_unpooled_overload_remains_independent(self):
        parser = self.parser()
        reader = Reader(bytes([7, 33, 99]))
        row = parser.decode('EntityCfg', reader)
        self.assertEqual(dict(row), dict(Value=7, Group=33, Entity=99))
        self.assertEqual(reader.pos, 3)
        # The same interpreter keeps separate compiled overloads.
        pooled = parser.decode('EntityCfg', Reader(bytes([0, 2, 3])), [{413: 1}])
        self.assertEqual(pooled.Factors, {413: 1})
        self.assertNotIn('Value', pooled)


if __name__ == '__main__':
    unittest.main()
