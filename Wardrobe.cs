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
        UIDynamicButton applyButton, dumpButton;
        JSONStorableBool asDiffuse, asCutout, asBump, asGloss, asNormal;
        UIDynamicToggle asDiffuseButton, asCutoutButton, asBumpButton, asGlossButton, asNormalButton;
        StorableReplacements replacements;

        // Indicate whether loading from the JSON has completed.
        // Initial load of textures must wait until the clothes have all been loaded,
        // which is not the case by the time of `Start` on a fresh start of VaM.
        private bool needsLoad;

        // Runtime use variables
        private DAZClothingItem          myClothes;
        private DAZSkinWrap              mySkin;
        private Material                 myMaterial;
        private List< TextureReference > textureReferences;
        private TextureReference         textureFile;

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
                // TODO: Add some width to the dropdown.

                // Create the slot in which all changed textures are stored.
                replacements = new StorableReplacements();
                RegisterString( replacements );

                // Create the import options
                asDiffuse = new JSONStorableBool( "Diffuse texture", true );
                asCutout  = new JSONStorableBool( "Cutout", true );
                asBump    = new JSONStorableBool( "Bump map", false );
                asGloss   = new JSONStorableBool( "Glossy", false );
                asNormal  = new JSONStorableBool( "Normal map", false );
                asDiffuseButton = CreateToggle( asDiffuse );
                asCutoutButton  = CreateToggle( asCutout );
                asBumpButton    = CreateToggle( asBump );
                asGlossButton   = CreateToggle( asGloss );
                asNormalButton  = CreateToggle( asNormal );

                // TODO: Currently disabled:
                asNormalButton.toggle.interactable = false;

                // Action to perform replacement
                applyButton = CreateButton( "Apply" );
                applyButton.button.onClick.AddListener( ApplyTexture );
                
                // Create a dump button
                UIDynamic align = CreateSpacer( true );
                align.height = 25;
                dumpButton = CreateButton("Dump OBJ and MTL files - look in root", true);
                if (dumpButton != null)
                {
                    dumpButton.button.onClick.AddListener(DumpButtonCallback);
                    dumpButton.button.interactable = false;
                }
            }
            catch( Exception ex )
            {
                SuperController.LogError( $"Could not initialize Wardrobe {ex}" );
            }
        }

        public void Update()
        {
            try
            {
                if( needsLoad && ! SuperController.singleton.isLoading )
                {
                    // Load all the previously saved replacements
                    foreach( var entry in replacements.All() )
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

                    /* TEMPORARY (probing for shader properties. * /
                    string propertyToTest = "_NormalMap";
                    DAZClothingItem tc = GameObject.FindObjectsOfType< DAZClothingItem >().First();
                    DAZSkinWrap ts = tc.GetComponentsInChildren< DAZSkinWrap >().First();
                    Material tm = ts.GPUmaterials.First();
                    if( tm.HasProperty( propertyToTest ) )
                        SuperController.LogMessage( "Found " + propertyToTest );
                    else
                        SuperController.LogMessage( "Did not find " + propertyToTest );
                    // */
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

        private void SelectClothingItem( string clothingName )
        {
            SelectSkinWrap( null );
            if( clothingName == null )
            {
                myClothes = null;
                List< string > clothings = GameObject
                    .FindObjectsOfType< DAZClothingItem >()
                    .Where( dci => dci.containingAtom == myPerson )
                    .Select( dci => dci.name )
                    .ToList();
                clothings.Insert( 0, "REFRESH" );
                clothingItems.choices = clothings;

                // No clothing selected, disable dumping OBJs.
                dumpButton.button.interactable = false;
            }
            else if( clothingName == "REFRESH" )
            {
                // call us again with no value.
                clothingItems.val = null;
            }
            else
            {
                myClothes = FindObjectsOfType< DAZClothingItem >()
                    .Where( dci => dci.containingAtom == myPerson )
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

                // Enable the button to dump the OBJs with UVs intact.
                dumpButton.button.interactable = true;
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
                    .Select( tr => tr.Abbreviation )
                    .ToList();

                if( textureReferences.Count == 1 )
                {
                    // Pre-select the single texture available
                    textures.val = textureReferences.ElementAt( 0 ).Abbreviation;
                }

            }
        }

        private void SelectTexture( string byDisplayName )
        {
            if( byDisplayName == null )
            {
                textures.val = null;
                applyButton.button.enabled = false;
            }
            else
            {
                TextureReference filename = textureReferences
                    .Where( tr => tr.Abbreviation == byDisplayName )
                    .First();

                textureFile = filename;
                applyButton.button.enabled = true;
            }
        }

        private void ApplyTexture()
        {
            // Collect the shader textures to which we apply
            List< string > texmap = new List< string >();
            if( asDiffuse.val )
                texmap.Add( PROP_DIFFUSE );
            if( asCutout.val )
                texmap.Add( PROP_CUTOUT );
            if( asBump.val )
                texmap.Add( PROP_BUMP );
            if( asGloss.val )
                texmap.Add( PROP_GLOSS );

            // Let the user know they need to replace *something*
            if( texmap.Count == 0 )
            {
                SuperController.LogMessage( "Select which texture(s) to replace before applying." );
                return;
            }

            // Load the image and apply it to the material.
            var mat = myMaterial; // scope the closure locally.
            var img = new ImageLoaderThreaded.QueuedImage();
            img.imgPath = textureFile.filename;
            img.callback = qimg => SetTexture( texmap, mat, qimg );
            ImageLoaderThreaded.singleton.QueueImage( img );

            // Store this into the scene
            if( ! disableUpdate )
            {
                replacements.setTextureReplacement( $"{myClothes.name}/{mySkin.name}/{myMaterial.name}", textureFile, texmap );
            }
        }

        private void SetTexture( List< string > texmap, Material mat, ImageLoaderThreaded.QueuedImage texture )
        {
            if( texture.hadError )
            {
                SuperController.LogError( $"Error loading texture: {texture.errorText}" );
            }
            else
            {
                texmap.ForEach( name => mat.SetTexture( name, texture.tex ) );
            }

            // Now clear the UI
            clothingItems.val = null;
        }

        private void LoadSaved( StorableSlot slot, TextureReference full )
        {
            string[] components = slot.Material.Split( '/' );
            if( components.Length != 3 )
            {
                SuperController.LogError( $"Found badly formatted replacement material: {slot}" );
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
                    .Where( tr => tr.reference == full.reference )
                    .DefaultIfEmpty( null )
                    .FirstOrDefault();
                if( texref == null )
                {
                    SuperController.LogError( $"Texture missing '{full}'" );
                }
                else
                {
                    // TODO: Set the properties
                    SelectTexture( texref.Abbreviation );
                    ApplyTexture();
                }
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
                                   .Select( fp => TextureReference.fromReference( $"./{indir}/{fp}" ) ) );
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
                                   .Select( fp => TextureReference.fromReference( $"/{indir}/{fp}" ) ) );

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

        private class StorableReplacements : JSONStorableString
        {
            private Dictionary< StorableSlot, TextureReference > entries = new Dictionary< StorableSlot, TextureReference >();

            public StorableReplacements() : base( "replacements", "<placeholder>" )
            {
            }

            public void setTextureReplacement( string slot, TextureReference storedName, List< string > shaderProps )
            {
                foreach( var property in shaderProps )
                { 
                    entries[ new StorableSlot( slot, property ) ] = storedName;
                }
            }

            public IEnumerable< KeyValuePair< StorableSlot, TextureReference > > All()
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
                entries = new Dictionary< StorableSlot, TextureReference >();
                if( ! jc.Keys.Contains( "version" ) )
                { 
                    // this is version 1, the undocumented
                    SuperController.LogMessage( "Detected version 1 texture replacements" );
                    ParseReplacementsV1( jc[ "replacements" ] as JSONClass );
                }
                else
                {
                    // Assume the most recent version.
                    ParseReplacements( jc[ "replacements" ] as JSONArray );
                }
            }

            private void ParseReplacements( JSONArray replacements )
            {
                foreach( JSONClass obj in replacements )
                {
                    entries[ new StorableSlot( obj["slot"], obj["shader"] ) ] =
                        TextureReference.fromReference( obj["texture"].Value );
                }
            }
            
            public override bool StoreJSON( JSONClass jc, bool includePhysical = true, bool includeAppearance = true )
            {
                var replacements = new JSONArray();
                foreach( var kvp in entries )
                {
                    JSONClass obj = new JSONClass();
                    obj["slot"] = kvp.Key.Material;
                    obj["shader"] = kvp.Key.Property;
                    obj["texture"] = kvp.Value.reference;
                    replacements.Add( obj );
                }

                jc.Add( "version", new JSONData( 2 ) );
                jc.Add( "replacements", replacements );
                return true;
            }

            //
            // Legacy support parsers

            private void ParseReplacementsV1( JSONClass replacements )
            {
                foreach( JSONNode key in replacements.Keys )
                {
                    TextureReference texref = TextureReference.fromReference( replacements[key] );
                    entries[ new StorableSlot( key, PROP_DIFFUSE ) ] = texref;
                    entries[ new StorableSlot( key, PROP_CUTOUT ) ] = texref;
                }
            }
        }

        private class StorableSlot
        {
            public readonly string Material;
            public readonly string Property;

            public StorableSlot( string material, string property )
            {
                this.Material = material;
                this.Property = property;
            }

            public override bool Equals( object obj )
            {
                if( obj is StorableSlot )
                {
                    StorableSlot s = (StorableSlot)obj;
                    return s.Material == Material &&
                        s.Property == Property;
                }
                return false;
            }

            public override int GetHashCode()
            {
                const int hashbase = 7;
                const int hashmult = 13;
                int hash = hashbase;
                hash = (hash * hashmult) ^ (object.ReferenceEquals( Material, null ) ? 0 : Material.GetHashCode() );
                hash = (hash * hashmult) ^ (object.ReferenceEquals( Property, null ) ? 0 : Material.GetHashCode() );
                return hash;
            }
        }

        private class TextureReference
        {
            public readonly bool local;
            public readonly string reference;
            public readonly string filename;
            public string Abbreviation
            {
                get {
                    var locality = local ? "<scene>" : "<global>";
                    var basename = reference.Split( '/' ).Last();
                    return $"{locality}/{basename}";
                }
            }

            public TextureReference( bool local, string reference, string filename )
            {
                this.local = local;
                this.reference = reference;
                this.filename = filename;
            }

            public override string ToString()
            {
                return reference;
            }

            public static TextureReference fromReference( string storedName )
            {
                bool local;
                string filename;
                if( storedName.StartsWith( "./" ) )
                {
                    local = true;
                    filename = $"{SuperController.singleton.currentLoadDir}/Textures/{storedName.Remove( 0, 2 )}";
                }
                else
                {
                    local = false;
                    filename = $"{SuperController.singleton.savesDir}/../Textures/{storedName.Remove( 0, 1 )}";
                }

                return new TextureReference( local, storedName, filename );
            }
        }

        private static List< string > EMPTY_CHOICES = new List< string >();
        private static readonly string PROP_DIFFUSE = "_MainTex";
        private static readonly string PROP_CUTOUT  = "_AlphaTex";
        private static readonly string PROP_BUMP    = "_BumpMap";
        private static readonly string PROP_GLOSS   = "_GlossTex";
    }

}
