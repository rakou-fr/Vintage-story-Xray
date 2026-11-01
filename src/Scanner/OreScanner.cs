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

        public void ScanBlocksAsync(OreConfig config, Action<List<BlockPos>, List<int>, List<string>, List<Entity>, List<int>, List<string>> onScanComplete)
        {
            if (capi.World.Player?.Entity == null || config.Targets.Count == 0) return;

            BlockPos playerPos = capi.World.Player.Entity.Pos.AsBlockPos.Copy();
            Vec3d playerVec = capi.World.Player.Entity.Pos.XYZ.Clone();
            int radius = config.ScanRadius;
            Entity[] loadedEntities = capi.World.LoadedEntities.Values.ToArray();

            // 1. PRÉPARATION ULTRA-RAPIDE (Sur le thread principal)
            // On pré-calcule un tableau d'identifiants pour ne comparer que des entiers (100x plus rapide que du texte)
            int maxBlockId = capi.World.Blocks.Count;
            bool[] isTargetBlock = new bool[maxBlockId];
            int[] targetBlockColors = new int[maxBlockId];
            string[] targetBlockNames = new string[maxBlockId];

            foreach (Block block in capi.World.Blocks)
            {
                if (block?.Code == null || block.Id == 0) continue;
                string key = "block:" + block.Code.Path;
                
                foreach (var target in config.Targets)
                {
                    if (target.Key.StartsWith("block:") && key.Contains(target.Key.Substring(6)))
                    {
                        isTargetBlock[block.Id] = true;
                        targetBlockColors[block.Id] = target.Value;
                        targetBlockNames[block.Id] = Lang.Get($"{block.Code.Domain}:block-{block.Code.Path}");
                        break;
                    }
                }
            }

            // Préparation des entités ciblées
            var targetEntities = config.Targets.Where(t => t.Key.StartsWith("entity:")).ToList();

            Task.Run(() =>
            {
                List<BlockPos> bPositions = new List<BlockPos>();
                List<int> bColors = new List<int>();
                List<string> bNames = new List<string>();

                List<Entity> eEntities = new List<Entity>();
                List<int> eColors = new List<int>();
                List<string> eNames = new List<string>();

                // --- 2. RECHERCHE DES BLOCS PAR CHUNKS ---
                int chunkSize = GlobalConstants.ChunkSize; 
                
                // Définition de la zone de recherche brute
                int minX = playerPos.X - radius;
                int maxX = playerPos.X + radius;
                int minY = playerPos.Y - radius;
                int maxY = playerPos.Y + radius;
                int minZ = playerPos.Z - radius;
                int maxZ = playerPos.Z + radius;

                // Conversion en coordonnées de Chunks
                int minCX = minX / chunkSize;
                int maxCX = maxX / chunkSize;
                int minCY = minY / chunkSize;
                int maxCY = maxY / chunkSize;
                int minCZ = minZ / chunkSize;
                int maxCZ = maxZ / chunkSize;

                // CORRECTION 2 : Ajout de la dimension du joueur pour éviter l'avertissement d'obsolescence
                BlockPos pos = new BlockPos(playerPos.dimension);

                for (int cx = minCX; cx <= maxCX; cx++)
                {
                    for (int cy = minCY; cy <= maxCY; cy++)
                    {
                        for (int cz = minCZ; cz <= maxCZ; cz++)
                        {
                            // L'ASTUCE MAGIQUE : Si le chunk n'existe pas/n'est pas chargé, on l'ignore d'un coup !
                            if (capi.World.BlockAccessor.GetChunk(cx, cy, cz) == null) continue;

                            // Délimitation des bords pour ne pas déborder du rayon de base
                            int startX = Math.Max(minX, cx * chunkSize);
                            int endX = Math.Min(maxX, cx * chunkSize + chunkSize - 1);
                            
                            int startY = Math.Max(minY, cy * chunkSize);
                            int endY = Math.Min(maxY, cy * chunkSize + chunkSize - 1);
                            
                            int startZ = Math.Max(minZ, cz * chunkSize);
                            int endZ = Math.Min(maxZ, cz * chunkSize + chunkSize - 1);

                            // Boucle sur les blocs uniquement dans les chunks valides
                            for (int x = startX; x <= endX; x++)
                            {
                                for (int y = startY; y <= endY; y++)
                                {
                                    for (int z = startZ; z <= endZ; z++)
                                    {
                                        pos.Set(x, y, z);
                                        int blockId = capi.World.BlockAccessor.GetBlockId(pos); // Très léger

                                        // Accès direct O(1) : Est-ce un bloc ciblé ?
                                        if (blockId > 0 && blockId < isTargetBlock.Length && isTargetBlock[blockId])
                                        {
                                            bPositions.Add(pos.Copy()); // On alloue la RAM *uniquement* si on a trouvé
                                            bColors.Add(targetBlockColors[blockId]);
                                            bNames.Add(targetBlockNames[blockId]);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // --- 3. RECHERCHE DES ENTITÉS ---
                foreach (Entity entity in loadedEntities)
                {
                    if (entity?.Code == null || !entity.Alive) continue;

                    if (entity.Pos.XYZ.DistanceTo(playerVec) <= radius)
                    {
                        string key = "entity:" + entity.Code.Path;
                        foreach (var target in targetEntities)
                        {
                            if (key.Contains(target.Key.Substring(7)))
                            {
                                eEntities.Add(entity);
                                eColors.Add(target.Value);
                                
                                string eName = Lang.Get($"{entity.Code.Domain}:item-creature-{entity.Code.Path}");
                                eNames.Add(eName.Contains("item-creature") ? entity.Code.Path : eName);
                                break;
                            }
                        }
                    }
                }

                // Retour au thread principal pour l'affichage
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    onScanComplete?.Invoke(bPositions, bColors, bNames, eEntities, eColors, eNames);
                }, "updateOreHighlights");
            });
        }
    }
}