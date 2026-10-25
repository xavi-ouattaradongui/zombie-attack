using UnityEditor;
using UnityEngine;

namespace ZombieRun.EditorTools
{
    /// <summary>
    /// Réglages automatiques à l'import :
    /// - personnages Mixamo (Characters) : squelette Humanoid, animations sur place, boucles ;
    /// - modèles Kenney (Environment) : pas d'animation, pas de caméras ni lumières.
    /// </summary>
    public class ZRImporter : AssetPostprocessor
    {
        string P => assetPath.Replace('\\', '/');
        bool IsCharacter => P.Contains("/ZombieRun/Characters/") && P.ToLower().EndsWith(".fbx");
        bool IsEnvironment => P.Contains("/ZombieRun/Environment/") && P.ToLower().EndsWith(".fbx");

        void OnPreprocessModel()
        {
            var imp = (ModelImporter)assetImporter;
            if (IsCharacter)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                ClearStaleBoneMapping(imp);
                imp.importCameras = false;
                imp.importLights = false;
                imp.importAnimation = P.Contains("@");
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
            else if (IsEnvironment)
            {
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.importBlendShapes = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
        }

        /// <summary>
        /// Si le fichier a été remplacé par un autre squelette Mixamo (ex. « mixamorig: » au lieu de « mixamorig6: »),
        /// l'ancien mappage d'os resté dans le .meta casse l'avatar. On l'efface pour que Unity le refasse.
        /// </summary>
        void ClearStaleBoneMapping(ModelImporter imp)
        {
            var desc = imp.humanDescription;
            if (desc.human == null || desc.human.Length == 0) return;
            string text;
            try { text = System.Text.Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(P)); }
            catch { return; }
            foreach (var hb in desc.human)
            {
                if (string.IsNullOrEmpty(hb.boneName) || text.Contains(hb.boneName)) continue;
                Debug.Log($"[Zombie Run] Squelette changé dans {P} : mappage des os refait.");
                imp.humanDescription = new HumanDescription
                {
                    armStretch = 0.05f, legStretch = 0.05f, upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f, lowerLegTwist = 0.5f, feetSpacing = 0f, hasTranslationDoF = false,
                    human = new HumanBone[0], skeleton = new SkeletonBone[0]
                };
                return;
            }
        }

        /// <summary>Images de l'écran d'accueil et du logo : en sprites pour l'interface.</summary>
        void OnPreprocessTexture()
        {
            // Version web : les textures des persos et zombies sont réduites (téléchargement plus léger)
            if (P.Contains("/ZombieRun/Characters/"))
            {
                var ti = (TextureImporter)assetImporter;
                var web = ti.GetPlatformTextureSettings("WebGL");
                web.overridden = true;
                web.maxTextureSize = 1024;
                web.format = TextureImporterFormat.Automatic;
                web.textureCompression = TextureImporterCompression.Compressed;
                ti.SetPlatformTextureSettings(web);
                return;
            }
            if (!P.Contains("/ZombieRun/UI/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.maxTextureSize = 2048;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            // Images étirables (9-slice) : bords gauche, bas, droit, haut en pixels
            var n = System.IO.Path.GetFileNameWithoutExtension(P);
            if (n == "TileBlank") t.spriteBorder = new Vector4(96, 56, 96, 56);
            else if (n == "WideBlank") t.spriteBorder = new Vector4(70, 56, 70, 56);
            else if (n == "PillBody") t.spriteBorder = new Vector4(90, 0, 90, 0);
        }

        /// <summary>Musiques : compressées et lues en continu (légères en mémoire, même sur mobile).</summary>
        void OnPreprocessAudio()
        {
            if (!P.Contains("/ZombieRun/Audio/")) return;
            var a = (AudioImporter)assetImporter;
            var st = a.defaultSampleSettings;
            st.loadType = AudioClipLoadType.Streaming;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = 0.6f;
            a.defaultSampleSettings = st;
        }

        void OnPreprocessAnimation()
        {
            if (!IsCharacter || !P.Contains("@")) return;
            var imp = (ModelImporter)assetImporter;
            var clips = imp.defaultClipAnimations;
            string n = P.ToLower();
            bool loop = n.Contains("@run") || n.Contains("@sprint") || n.Contains("@idle") || n.Contains("@attack");
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                // Déplacement avant/arrière de l'animation retiré (sinon la glissade ramène le perso en arrière).
                c.lockRootPositionXZ = false;
                c.keepOriginalOrientation = true;
                // Hauteur calée sur les pieds : le perso reste posé sur la route, quelle que soit l'animation.
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;
                c.keepOriginalPositionXZ = false;
                clips[i] = c;
            }
            imp.clipAnimations = clips;
        }
    }
}
