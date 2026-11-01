using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace OreHighlight
{
    public class OreRenderer : IRenderer, IDisposable
    {
        private ICoreClientAPI capi;
        public bool IsActive = true;
        // Listes pour les Blocs (statiques)
        private List<BlockPos> positions = new List<BlockPos>();
        private List<int> colors = new List<int>();
        private List<string> names = new List<string>();

        // Listes pour les Entités (dynamiques)
        private List<Entity> entities = new List<Entity>();
        private List<int> entityColors = new List<int>();
        private List<string> entityNames = new List<string>();

        public double RenderOrder => 0.7;
        public int RenderRange => 5000;

        private LoadedTexture boxTexture;
        private LoadedTexture hoverTextTexture;
        private string currentHoverText = "";

        public OreRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "ore-esp-2d");
            
            boxTexture = api.Gui.TextTexture.GenUnscaledTextTexture(" ", CairoFont.WhiteSmallText(), new TextBackground
            {
                FillColor = ColorUtil.WhiteArgbDouble,
                Padding = 10,
                Radius = 0,
                Shade = false,
                BorderWidth = 0
            });
        }

        // Mise à jour de la signature pour recevoir les blocs ET les entités
        public void UpdateHighlights(
            List<BlockPos> bPos, List<int> bCol, List<string> bNames,
            List<Entity> eEnt, List<int> eCol, List<string> eNames)
        {
            this.positions = bPos;
            this.colors = bCol;
            this.names = bNames;
            
            this.entities = eEnt;
            this.entityColors = eCol;
            this.entityNames = eNames;
        }

        public void ClearHighlights()
        {
            this.positions.Clear();
            this.colors.Clear();
            this.names.Clear();
            this.entities.Clear();
            this.entityColors.Clear();
            this.entityNames.Clear();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!IsActive) return;
            if (capi.World.Player?.Entity == null) return;
            if (positions.Count == 0 && entities.Count == 0) return;

            float crossX = capi.Render.FrameWidth / 2f;
            float crossY = capi.Render.FrameHeight / 2f;
            
            string bestHoverText = null;
            float closestDistance = float.MaxValue;

            // --- FONCTION LOCALE POUR ÉVITER DE RÉPÉTER LE CODE MATHÉMATIQUE ---
            void DrawBoxAndCheckHover(Vec3d center, int color, string name)
            {
                Vec3d screenPos = MatrixToolsd.Project(
                    center, 
                    capi.Render.PerspectiveProjectionMat, 
                    capi.Render.PerspectiveViewMat, 
                    capi.Render.FrameWidth, 
                    capi.Render.FrameHeight
                );

                if (screenPos.Z < 0) return;

                float screenX = (float)screenPos.X;
                float screenY = (float)capi.Render.FrameHeight - (float)screenPos.Y;

                float distance = (float)capi.World.Player.Entity.Pos.DistanceTo(center);
                
                float boxSize = (float)capi.Render.FrameHeight / Math.Max(1f, distance * 1.5f);
                boxSize = GameMath.Clamp(boxSize, 4f, capi.Render.FrameHeight / 2f);

                Vec4f tintColor = ColorUtil.ToRGBAVec4f(color);
                
                capi.Render.Render2DTexture(
                    boxTexture.TextureId, 
                    screenX - (boxSize / 2f), 
                    screenY - (boxSize / 2f), 
                    boxSize, 
                    boxSize, 
                    1000f, 
                    tintColor
                );
                
                if (Math.Abs(screenX - crossX) < (boxSize / 2f) && Math.Abs(screenY - crossY) < (boxSize / 2f))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        bestHoverText = $"{name}\n{distance:0.0} m";
                    }
                }
            }

            // 1. Affichage des Blocs
            for (int i = 0; i < positions.Count; i++)
            {
                Vec3d blockCenter = new Vec3d(positions[i].X + 0.5, positions[i].Y + 0.5, positions[i].Z + 0.5);
                DrawBoxAndCheckHover(blockCenter, colors[i], names[i]);
            }

            // 2. Affichage des Entités (Mise à jour fluide en temps réel)
            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                if (!entity.Alive) continue; // Si l'animal est tué ou disparait, on ne le dessine plus

                Vec3d entityCenter = new Vec3d(entity.Pos.X, entity.Pos.Y + 0.5, entity.Pos.Z);
                DrawBoxAndCheckHover(entityCenter, entityColors[i], entityNames[i]);
            }

            // --- AFFICHAGE DU TEXTE AU SURVOL ---
            if (bestHoverText != null)
            {
                if (bestHoverText != currentHoverText)
                {
                    currentHoverText = bestHoverText;
                    hoverTextTexture?.Dispose();
                    
                    CairoFont font = CairoFont.WhiteSmallText().WithStroke(new double[] { 0, 0, 0, 1 }, 2);
                    hoverTextTexture = capi.Gui.TextTexture.GenUnscaledTextTexture(
                        currentHoverText, 
                        font, 
                        new TextBackground { FillColor = new double[] { 0, 0, 0, 0.6 }, Padding = 5, Radius = 3 }
                    );
                }
                
                capi.Render.Render2DTexture(
                    hoverTextTexture.TextureId, 
                    crossX + 15, 
                    crossY + 15, 
                    hoverTextTexture.Width, 
                    hoverTextTexture.Height, 
                    10000f
                );
            }
        }

        public void Dispose()
        {
            boxTexture?.Dispose();
            hoverTextTexture?.Dispose();
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        }
    }
}