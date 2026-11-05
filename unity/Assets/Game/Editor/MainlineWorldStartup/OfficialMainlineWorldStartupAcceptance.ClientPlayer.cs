using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private static IEnumerator VerifyClientPlayer()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            using (var logging = new NativeMap14SharedLogging(shared))
            {
                object propertyParser = null;
                long? expectedCombat = null;
                using (var configuration = new NativeMap14SharedConfiguration(shared))
                using (var services = new MainlineSharedClientServices(shared))
                {
                    MainlineSharedClientConfiguration.Load(shared, configuration.Manager);
                    propertyParser = configuration.Manager.GetParser(shared.CommonType("LevelPropParser"));
                    try
                    {
                        MainlineSharedClientConfiguration.Load(shared, configuration.Manager);
                        throw new InvalidDataException("Duplicate player table registration was accepted.");
                    }
                    catch (InvalidOperationException) { }
                    const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
                    var reader = propertyParser.GetType().GetField("mBinaryReader", fields).GetValue(propertyParser);
                    var stream = reader.GetType().GetField("mStream", fields).GetValue(reader) as FileStream;
                    if (stream == null || !stream.CanRead || stream.CanWrite)
                        throw new InvalidDataException("The original property reader did not open the local read-only file.");
                    var playerProperty = services.Manager.GetType().GetProperty("Player");
                    if (playerProperty.GetValue(services.Manager) != null)
                        throw new InvalidDataException("Fresh client validation unexpectedly found a live player.");
                    for (int index = 0; index < 2; index++)
                    {
                        var player = Activator.CreateInstance(shared.CommonType("Player"));
                        try
                        {
                            // This setter executes the entire original ClientInit,
                            // property chunks, pet calculation and account setup.
                            playerProperty.SetValue(services.Manager, player);
                            if (!(bool)NativeMap14SharedPrefabs.Get(player, "Inited"))
                                throw new InvalidDataException("Original Player.ClientInit did not complete.");
                            var center = NativeMap14SharedPrefabs.Get(player, "PlayerPropCenter");
                            var contexts = (IDictionary)NativeMap14SharedPrefabs.Get(center, "PlayerPropContexts");
                            if (contexts.Count != 2)
                                throw new InvalidDataException("Original normal and no-bless property contexts were not initialized.");
                            var battle = center.GetType().GetMethod("GetBattleProp", Type.EmptyTypes);
                            string before = PropertyValues((IDictionary)battle.Invoke(center, null));
                            long combat = (long)NativeMap14SharedPrefabs.Get(player, "CombatRating");
                            center.GetType().GetMethod("UpdateAllPropChuncks").Invoke(center, null);
                            if (before.Length == 0 || before != PropertyValues((IDictionary)battle.Invoke(center, null))
                                || combat != (long)NativeMap14SharedPrefabs.Get(player, "CombatRating"))
                                throw new InvalidDataException("Original player property recalculation is not stable.");
                            if (expectedCombat.HasValue && expectedCombat.Value != combat)
                                throw new InvalidDataException("Independent fresh players produced different original combat ratings.");
                            expectedCombat = combat;
                            Debug.Log("MAINLINE_CLIENT_PLAYER_INITIALIZED: original combat=" + combat);
                            yield return null;
                        }
                        finally
                        {
                            // The original setter invokes ClientDestory; keep the
                            // parser and formula scope alive until it returns.
                            playerProperty.SetValue(services.Manager, null);
                        }
                        if ((bool)NativeMap14SharedPrefabs.Get(player, "Inited"))
                            throw new InvalidDataException("Original Player.ClientDestory did not finish.");
                    }
                }
                if (propertyParser.GetType().GetField("mBinaryReader", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(propertyParser) != null)
                    throw new InvalidDataException("The map configuration scope leaked its original property reader.");
            }
            if (errors != 0) throw new InvalidDataException("Original client lifecycle logged errors: " + errors);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "client-player.json"),
                "{\"fresh_players\":2,\"original_client_init\":true,\"original_client_destroy\":true,\"original_property_contexts\":true,\"local_binary_reader_disposed\":true,\"play_mode\":true}");
            Debug.Log("MAINLINE_CLIENT_PLAYER_PASS: full original initialization, recalculation and disposal.");
        }

        private static string PropertyValues(IDictionary values)
        {
            return string.Join(";", values.Keys.Cast<object>().OrderBy(Convert.ToInt32)
                .Select(key => Convert.ToInt32(key) + "=" + values[key]));
        }
    }
}
