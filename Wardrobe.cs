using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/**
 * Cloth texture replacer.
 *
 * Replace
 *
 * Authors: chokaphi and VamDazzler
 * License: Creative Commons with Attribution (CC BY 3.0)
 *
 * History:
 * Jan 26, 2019 chokaphi: Proof of concept. Single clothing item, fixed file.
 * Jan 27, 2019 VamDazzler: Generalization, UI, and transparency fix.
 * Jan 28, 2019 VamSander: Added an export obj button.
 */
namespace chokaphi_VamDazz
{
    public class Wardrobe : MVRScript
    {
        private bool disableUpdate;

        //person script is attatched too
        Atom myPerson;
        JSONStorableStringChooser clothingItems, skinWraps, materials, textures;
        UIDynamicButton dumpButton;
        StorableReplacements replacements;

        public override void Init()
        {
            try
            {
                disableUpdate = true;
                pluginLabelJSON.val = "Wardrobe (by VamDazzler and chokaphi)";

                // Obtain our person
                myPerson = containingAtom;
                if( myPerson == null )
                {
                    SuperController.LogError("Please add this plugin to a PERSON atom.");
                    throw new Exception( "Halting Wardrobe due to de-Atom-ization" );
                }

                // Create the clothing items drop-down
                clothingItems = new JSONStorableStringChooser( "clothing", EMPTY_CHOICES, null, "Clothing Item" );
                UIDynamicPopup clothingSelector = CreateScrollablePopup( clothingItems );

                // Create the skinning drop-down
                skinWraps = new JSONStorableStringChooser( "skin", EMPTY_CHOICES, null, "Skin" );
                UIDynamicPopup skinSelector = CreateScrollablePopup( skinWraps );

                // Create the materials drop-down
                materials = new JSONStorableStringChooser( "material", EMPTY_CHOICES, null, "Material" );
                UIDynamicPopup materialSelector = CreateScrollablePopup( materials );

                // Create the texture selector
                textures = new JSONStorableStringChooser( "texture", EMPTY_CHOICES, null, "Texture" );
                UIDynamicPopup textureSelector = CreateScrollablePopup( textures );

                // Create the slot in which all changed textures are stored.
                replacements = new StorableReplacements();
                RegisterString( replacements );

                // Create a dump button
                dumpButton = CreateButton("Dump obj file - look in root");
                if (dumpButton != null)
                {
                    dumpButton.button.onClick.AddListener(DumpButtonCallback);
                    EnableDumpButton(false);
                }
            }
            catch( Exception ex )
            {
                SuperController.LogError( $"Could not initialize Wardrobe {ex}" );
            }
        }

        private bool needsLoad;

        public void Update()
        {
            try
            {
                if( needsLoad && ! SuperController.singleton.isLoading )
                {
                    // Load all the previously saved replacements
                    foreach( KeyValuePair< string, string > entry in replacements.All() )
                    {
                        try
                        {
                            LoadSaved( entry.Key, entry.Value );
                        }
                        catch( Exception ex )
                        {
                            SuperController.LogError( $"Could not load saved texture for {entry.Key} {ex}" );
                        }
                        catch
                        {
                            SuperController.LogError( $"Could not load saved texture for {entry.Key} (unknown reason)" );
                        }
                    }

                    // Reset the UI (cascades)
                    SelectClothingItem( null );

                    // Allow updates to occur normally.
                    disableUpdate = false;
                    needsLoad = false;
                }
            }
            catch( Exception ex )
            {
                if( ! disableUpdate )
                {
                    SuperController.LogError( "Error while updating " + ex );
                    disableUpdate = true;
                }
            }
        }

        void Start()
        {
            try
            {
                // No point if we don't have a person.
                if( myPerson == null )
                    return;

                // Now that loading is complete, set our UI callbacks
                clothingItems.setCallbackFunction = this.SelectClothingItem;
                skinWraps.setCallbackFunction = this.SelectSkinWrap;
                materials.setCallbackFunction = this.SelectMaterial;
                textures.setCallbackFunction = this.SelectTexture;
                SelectClothingItem( null );

                needsLoad = true;
            }
            catch( Exception ex )
            {
                SuperController.LogError( $"Could not start Wardrobe {ex}" );
                disableUpdate = true;
            }
        }

        private DAZClothingItem          myClothes;
        private DAZSkinWrap              mySkin;
        private Material                 myMaterial;
        private List< TextureReference > textureReferences;

        private void SelectClothingItem( string clothingName )
        {
            SelectSkinWrap( null );
            if( clothingName == null )
            {
                myClothes = null;
                List< string > clothings = GameObject
                    .FindObjectsOfType< DAZClothingItem >()
                    .Select( dci => dci.name )
                    .ToList();
                clothings.Insert( 0, "REFRESH" );
                clothingItems.choices = clothings;
                EnableDumpButton(false);
            }
            else if( clothingName == "REFRESH" )
            {
                // call us again with no value.
                clothingItems.val = null;
            }
            else
            {
                myClothes = FindObjectsOfType< DAZClothingItem >()
                    .Where( dci => dci.name == clothingName )
                    .First();

                List< string > skinChoices = myClothes
                    .GetComponentsInChildren< DAZSkinWrap >()
                    .Select( sc => sc.name )
                    .ToList();

                skinWraps.choices = skinChoices;

                if( skinChoices.Count == 1 )
                {
                    // Pre-select if there's only one skin
                    skinWraps.val = skinChoices.ElementAt( 0 );
                }

                EnableDumpButton(true);
            }
        }

        private void SelectSkinWrap( string byName )
        {
            SelectMaterial( null );
            if( byName == null )
            {
                mySkin = null;
                skinWraps.choices = EMPTY_CHOICES;
                skinWraps.valNoCallback = null;
            }
            else
            {
                mySkin = myClothes.GetComponentsInChildren< DAZSkinWrap >()
                    .Where( dsw => dsw.name == byName)
                    .FirstOrDefault();

                List< string > materialNames = mySkin.GPUmaterials
                    .Select( mat => mat.name )
                    .ToList();

                materials.choices = materialNames;

                if( materialNames.Count == 1 )
                {
                    // Pre-select the single material.
                    materials.val = materialNames.ElementAt( 0 );
                }
            }
        }

        private void SelectMaterial( string byName )
        {
            SelectTexture( null );
            if( byName == null )
            {
                myMaterial = null;
                materials.choices = EMPTY_CHOICES;
                materials.valNoCallback = null;
            }
            else
            {
                myMaterial = mySkin.GPUmaterials
                    .Where( mat => mat.name == byName )
                    .First();

                string subdir = $"{myClothes.name}/{mySkin.name}";
                textureReferences = FindTextures( subdir, myMaterial.name );
                textures.choices = textureReferences
                    .Select( tr => tr.displayName )
                    .ToList();

                // Note: Don't preselect here, because found texture
                //       may not be what the user is expecting.
            }
        }

        private void SelectTexture( string byDisplayName )
        {
            if( byDisplayName == null )
            {
                textures.val = null;
            }
            else
            {
                string filename = textureReferences
                    .Where( tr => tr.displayName == byDisplayName )
                    .Select( tr => tr.filename )
                    .First();

                // Load the image and apply it to the material.
                var mat = myMaterial; // scope the closure locally.
                var img = new ImageLoaderThreaded.QueuedImage();
                img.imgPath = filenameFromStoreName( filename );
                img.callback = qimg => SetTexture( mat, qimg );
                ImageLoaderThreaded.singleton.QueueImage( img );

                // Store this into the scene
                if( ! disableUpdate )
                {
                    replacements.setTextureReplacement( $"{myClothes.name}/{mySkin.name}/{myMaterial.name}", filename );
                }
            }
        }

        private void SetTexture( Material mat, ImageLoaderThreaded.QueuedImage texture )
        {
            if( texture.hadError )
            {
                SuperController.LogError( $"Error loading texture: {texture.errorText}" );
            }
            else
            {
                mat.mainTexture = texture.tex;
                mat.SetTexture( "_AlphaTex", texture.tex );
            }

            // Now clear the UI
            clothingItems.val = null;
        }

        private void LoadSaved( string slot, string full )
        {
            string[] components = slot.Split( '/' );
            if( components.Length != 3 )
            {
                SuperController.LogError( $"Found badly formatted replacement: {slot}" );
            }
            else
            {
                SelectClothingItem( components.ElementAt( 0 ) );
                if( myClothes == null )
                    throw new Exception( $"Could not get clothes '{components.ElementAt( 0 )}'" );

                SelectSkinWrap( components.ElementAt( 1 ) );
                if( mySkin == null )
                    throw new Exception( $"Could not get skin '{components.ElementAt( 1 )}'");

                SelectMaterial( components.ElementAt( 2 ) );
                if( myMaterial == null )
                    throw new Exception( $"Could not get material '{components.ElementAt( 2 )}'" );

                // The list of references should now be populated.
                TextureReference texref = textureReferences
                    .Where( tr => tr.filename == full )
                    .DefaultIfEmpty( null )
                    .FirstOrDefault();
                if( texref == null )
                {
                    SuperController.LogError( $"Texture missing '{full}'" );
                }
                else
                {
                    SelectTexture( texref.displayName );
                }
            }
        }

        private static string filenameFromStoreName( string storedName )
        {
            if( storedName.StartsWith( "./" ) )
            {
                return $"{SuperController.singleton.currentLoadDir}/Textures/{storedName.Remove( 0, 2 )}";
            }
            else
            {
                return $"{SuperController.singleton.savesDir}/../Textures/{storedName.Remove( 0, 1 )}";
            }
        }

        private List< TextureReference > FindTextures( string indir, string withbasename )
        {
            List< TextureReference > textures = new List< TextureReference >();

            try
            {
                // Add scene-local files to the list.
                string dir = $"{SuperController.singleton.currentLoadDir}/Textures/{indir}";
                textures.AddRange( SuperController.singleton
                                   .GetFilesAtPath( dir )
                                   .Select( fp => fp.Remove( 0, dir.Length + 1 ) )
                                   .Where( fp => fp.StartsWith( withbasename ) )
                                   .Select( fp => new TextureReference( $"<scene>/{fp}", $"./{indir}/{fp}" ) ) );
            }
            catch
            {
                // This space intentionally blank
            }

            try
            {
                // Add global texture files to the list.
                string dir = $"{SuperController.singleton.savesDir}/../Textures/{indir}";
                textures.AddRange( SuperController.singleton
                                   .GetFilesAtPath( dir )
                                   .Select( fp => fp.Remove( 0, dir.Length + 1 ) )
                                   .Where( fp => fp.StartsWith( withbasename ) )
                                   .Select( fp => new TextureReference( $"<global>/{fp}", $"/{indir}/{fp}" ) ) );

            }
            catch
            {
                // This space intentionally blank
            }


            if( textures.Count() == 0 )
            {
                SuperController.LogMessage( "Could not find a replacement texture at either the scene or global Vam directory" );
                SuperController.LogMessage( $"To replace this material, place a texture file named '{withbasename}.[png|jpg]'in 'Textures/{indir}'" );
            }

            return textures;
        }

        private class TextureReference
        {
            public string displayName;
            public string filename;

            public TextureReference( string displayName, string filename )
            {
                this.displayName = displayName;
                this.filename = filename;
            }
        }

        private class StorableReplacements : JSONStorableString
        {
            private Dictionary< string, string > entries;

            public StorableReplacements() : base( "replacements", "<placeholder>" )
            {
                entries = new Dictionary<string, string>();
            }

            public void setTextureReplacement( string slot, string storedName )
            {
                entries[ slot ] = storedName;
            }

            public void Remove( string slot )
            {
                entries.Remove( slot );
            }

            public IEnumerable< KeyValuePair< string, string > > All()
            {
                return entries;
            }

            public override void LateRestoreFromJSON( JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true )
            {
                // This may not be necessary, I don't know the lifecycle of a JSONStorable well enough.
                RestoreFromJSON( jc, restorePhysical, restoreAppearance );
            }

            public override bool NeedsLateRestore( JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true )
            {
                return false;
            }

            public override bool NeedsRestore( JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true )
            {
                return true;
            }

            public override void RestoreFromJSON( JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true )
            {
                entries = new Dictionary<string, string>();
                JSONClass replacements = jc["replacements"] as JSONClass;
                if( replacements != null )
                {
                    foreach( String key in replacements.Keys )
                    {
                        entries.Add( key, replacements[key] );
                    }
                }
            }

            public override bool StoreJSON( JSONClass jc, bool includePhysical = true, bool includeAppearance = true )
            {
                var replacements = new JSONClass();
                foreach( KeyValuePair< string, string > kvp in entries )
                {
                    replacements.Add( kvp.Key, kvp.Value );
                }

                jc.Add( "replacements", replacements );
                return true;
            }
        }

        private static List< string > EMPTY_CHOICES = new List< string >();

        void EnableDumpButton(bool enable)
        {
            dumpButton.button.interactable = enable;
        }

        public void DumpButtonCallback()
        {
            if (myClothes == null)
            {
                SuperController.LogMessage("Select a clothing item first");
                return; // shouldn't get here
            }

            DAZSkinWrap[] skinWraps = myClothes.GetComponentsInChildren<DAZSkinWrap>(true);
            if (skinWraps==null)
            {
                SuperController.LogMessage("No Skin Wraps found");
                return;
            }

            OBJExporter exporter = new OBJExporter();
            for (int i=0;i<skinWraps.Length;i++)
            {
                DAZMesh mesh = skinWraps[i].dazMesh;
                // use the mesh and the built in OBJExporter
                exporter.Export(myClothes.name + i + ".obj", mesh.uvMappedMesh, mesh.uvMappedMesh.vertices, mesh.uvMappedMesh.normals, mesh.materials);
            }
        }
    }
}
