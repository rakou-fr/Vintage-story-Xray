using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace OreHighlight
{
    public class OreScanner
    {
        private ICoreClientAPI capi;

        public OreScanner(ICoreClientAPI api)
        {
            this.capi = api;
        }

        // La signature renvoie désormais 6 listes (3 pour les blocs, 3 pour les entités)
        public void ScanBlocksAsync(OreConfig config, Action<List<BlockPos>, List<int>, List<string>, List<Entity>, List<int>, List<string>> onScanComplete)
        {
            if (capi.World.Player?.Entity == null || config.Targets.Count == 0) return;

            BlockPos playerPos = capi.World.Player.Entity.Pos.AsBlockPos.Copy();
            
            Vec3d playerVec = new Vec3d(
                capi.World.Player.Entity.Pos.X, 
                capi.World.Player.Entity.Pos.Y, 
                capi.World.Player.Entity.Pos.Z
            );
            
            int radius = config.ScanRadius;
            Entity[] loadedEntities = capi.World.LoadedEntities.Values.ToArray();

            Task.Run(() =>
            {
                // Listes pour les blocs
                List<BlockPos> bPositions = new List<BlockPos>();
                List<int> bColors = new List<int>();
                List<string> bNames = new List<string>();

                // Listes pour les entités
                List<Entity> eEntities = new List<Entity>();
                List<int> eColors = new List<int>();
                List<string> eNames = new List<string>();

                // 1. RECHERCHE DES BLOCS
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            BlockPos pos = playerPos.AddCopy(x, y, z);
                            Block block = capi.World.BlockAccessor.GetBlock(pos);

                            if (block != null && block.Id != 0 && block.Code != null)
                            {
                                string key = "block:" + block.Code.Path;
                                
                                foreach (var target in config.Targets)
                                {
                                    if (target.Key.StartsWith("block:") && key.Contains(target.Key.Substring(6)))
                                    {
                                        bPositions.Add(pos);
                                        bColors.Add(target.Value);
                                        bNames.Add(Lang.Get($"{block.Code.Domain}:block-{block.Code.Path}"));
                                        break; 
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. RECHERCHE DES ENTITÉS
                foreach (Entity entity in loadedEntities)
                {
                    if (entity?.Code == null || !entity.Alive) continue;

                    Vec3d entityVec = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);

                    if (entityVec.DistanceTo(playerVec) <= radius)
                    {
                        string key = "entity:" + entity.Code.Path;
                        
                        foreach (var target in config.Targets)
                        {
                            if (target.Key.StartsWith("entity:") && key.Contains(target.Key.Substring(7)))
                            {
                                eEntities.Add(entity); // On envoie l'objet entier, plus sa position fixe !
                                eColors.Add(target.Value);
                                
                                string eName = Lang.Get($"{entity.Code.Domain}:item-creature-{entity.Code.Path}");
                                eNames.Add(eName.Contains("item-creature") ? entity.Code.Path : eName);
                                break;
                            }
                        }
                    }
                }

                // Envoi des 6 listes au jeu
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    onScanComplete?.Invoke(bPositions, bColors, bNames, eEntities, eColors, eNames);
                }, "updateOreHighlights");
            });
        }
    }
}