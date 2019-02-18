using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
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
        JSONStorableStringChooser clothingItems, materials, textures;
        UIDynamicButton applyButton, dumpButton;
        List< ShaderRefControl > supportedShaderProperties;
        StorableReplacements replacements;

        // Indicate whether loading from the JSON has completed.
        // Initial load of textures must wait until the clothes have all been loaded,
        // which is not the case by the time of `Start` on a fresh start of VaM.
        private bool needsLoad;

        // Runtime use variables
        private DAZClothingItem          myClothes;
        private string                   myMaterialName;
        private List< TextureReference > textureReferences;
        private TextureReference         textureFile;

        public override void Init()
        {
            try
            {
                disableUpdate = true;
                pluginLabelJSON.val = "Wardrobe v1.1.1 (by VamDazzler)";

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

                // Create the materials drop-down
                materials = new JSONStorableStringChooser( "material", EMPTY_CHOICES, null, "Material" );
                UIDynamicPopup materialSelector = CreateScrollablePopup( materials );

                // Create the texture selector
                textures = new JSONStorableStringChooser( "texture", EMPTY_CHOICES, null, "Texture" );
                UIDynamicPopup textureSelector = CreateScrollablePopup( textures );
                RectTransform panel = textureSelector.popup.popupPanel;
                panel.SetSizeWithCurrentAnchors( RectTransform.Axis.Horizontal, 400f );
                panel.pivot = new Vector2( 0.35f, 1.0f );

                // Create the slot in which all changed textures are stored.
                replacements = new StorableReplacements();
                RegisterString( replacements );

                // Create the import options
                supportedShaderProperties = new List< ShaderRefControl >();
                supportedShaderProperties.Add( new ShaderRefControl( this, "Diffuse texture", PROP_DIFFUSE, true ) );
                supportedShaderProperties.Add( new ShaderRefControl( this, "Alpha", PROP_CUTOUT, true ) );
                supportedShaderProperties.Add( new ShaderRefControl( this, "Normal map", PROP_NORMAL, false ) );
                supportedShaderProperties.Add( new ShaderRefControl( this, "Specular map", PROP_SPEC, false ) );
                supportedShaderProperties.Add( new ShaderRefControl( this, "Glossy", PROP_GLOSS, false ) );

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
            SelectMaterial( null );
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

                // Clear any masking of texture slots
                supportedShaderProperties.ForEach( ssp => ssp.ClearMask() );
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

                // Get the first example of a skin wrap
                // (they all have the same geometry, just deformed differently)
                DAZSkinWrap skinWrap = myClothes
                    .GetComponentsInChildren< DAZSkinWrap >()
                    .FirstOrDefault();

                // Obtain the list of materials for the skinwrap
                List< string > materialNames = skinWrap.GPUmaterials
                    .Select( mat => mat.name )
                    .ToList();

                // Make them available in the selector
                materials.choices = materialNames;

                dumpButton.button.interactable = true;

                if( materialNames.Count == 1 )
                {
                    // Pre-select the single material.
                    materials.val = materialNames.ElementAt( 0 );
                }
            }
        }

        private void SelectMaterial( string byName )
        {
            SelectTexture( null as string );
            if( byName == null )
            {
                myMaterialName = null;
                materials.choices = EMPTY_CHOICES;
                materials.valNoCallback = null;
            }
            else
            {
                // Get the material slot selected.
                Material theMaterial = myClothes
                    .GetComponentsInChildren< DAZSkinWrap >()
                    .First().GPUmaterials
                    .Where( mat => mat.name == byName )
                    .First();
                myMaterialName = byName;

                // Now search for textures which can be applied to this clothing item.
                string subdir = $"{myClothes.name}";
                textureReferences = FindTextures( subdir, theMaterial.name );
                textures.choices = textureReferences
                    .Select( tr => tr.Abbreviation )
                    .ToList();

                if( textureReferences.Count == 1 )
                {
                    // Pre-select the single texture available
                    textures.val = textureReferences.ElementAt( 0 ).Abbreviation;
                }

                // Now mask the available texture slots.
                supportedShaderProperties.ForEach( ssp => ssp.MaskMaterial( theMaterial ) );
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

        private void SelectTexture( TextureReference byReference )
        {
            if( byReference == null )
            { 
                SelectTexture( null as string );
            }
            else
            {
                textureFile = byReference;
                applyButton.button.enabled = true;
            }
        }

        private void ApplyTexture()
        {
            // Collect the shader textures to which we apply
            List< string > texmap = supportedShaderProperties
                .Where( ssp => ssp.val )
                .Select( ssp => ssp.propName )
                .ToList();

            // Let the user know they need to replace *something*
            if( texmap.Count == 0 )
            {
                SuperController.LogMessage( "Select which texture(s) to replace before applying." );
                return;
            }

            // Load the image and apply it to the material.
            var clothes = myClothes;
            var mat = myMaterialName; // scope the closure locally.
            var img = new ImageLoaderThreaded.QueuedImage();
            img.imgPath = textureFile.filename;
            img.callback = qimg => SetTexture( texmap, clothes, mat, qimg );
            ImageLoaderThreaded.singleton.QueueImage( img );

            // Store this into the scene
            if( ! disableUpdate )
            {
                replacements.setTextureReplacement( $"{myClothes.name}/{myMaterialName}", textureFile, texmap );
            }
        }

        private void SetTexture( List< string > texmap, DAZClothingItem clothes, string materialName, ImageLoaderThreaded.QueuedImage texture )
        {
            if( texture.hadError )
            {
                SuperController.LogError( $"Error loading texture: {texture.errorText}" );
            }
            else
            {
                // Apply the loaded texture to all the requested texture slots
                // on all the skin wraps (clothing states)
                clothes.GetComponentsInChildren< DAZSkinWrap >()
                    .SelectMany( wrap => wrap.GPUmaterials )
                    .Where( mat => mat.name == materialName ).ToList()
                    .ForEach( mat => texmap
                       .ForEach( name => mat.SetTexture( name, texture.tex ) ) );
            }

            // Now clear the UI
            textures.val = null;
        }

        private void LoadSaved( StorableSlot slot, TextureReference full )
        {
            string[] components = slot.Material.Split( '/' );
            if( components.Length != 2 )
            {
                SuperController.LogError( $"Found badly formatted replacement material: {slot.Material}" );
            }
            else
            {
                SelectClothingItem( components.ElementAt( 0 ) );
                if( myClothes == null )
                    throw new Exception( $"Could not get clothes '{components.ElementAt( 0 )}'" );

                SelectMaterial( components.ElementAt( 1 ) );
                if( myMaterialName == null )
                    throw new Exception( $"Could not get material '{components.ElementAt( 2 )}'" );

                SelectTexture( full );
                supportedShaderProperties.ForEach( srpc => srpc.val = (srpc.propName == slot.Property) );
                ApplyTexture();
            }
        }

        private void CollectTextures( List< TextureReference > toList, string prefix, string withBaseName, string inDir )
        {
            try
            { 
                string basename = withBaseName.ToLower();
                toList.AddRange( SuperController.singleton
                    .GetFilesAtPath( inDir )
                    .Select( fp => fp.Remove( 0, inDir.Length + 1 ) )
                    .Select( fp => fp.ToLower() ) // NOTE: this is not posix
                    .Where( fp => fp.StartsWith( basename ) || fp.StartsWith( "default" ) )
                    .Select( fp => TextureReference.fromReference( $"{prefix}/{fp}" ) ) );
            }
            catch
            {
                // This space intentionally blank (directory may not exist)
            }
        }

        private List< TextureReference > FindTextures( string indir, string withbasename )
        {
            List< TextureReference > textures = new List< TextureReference >();

            // Add scene-local files to the list.
            CollectTextures( textures, $"./{indir}", withbasename,
                $"{SuperController.singleton.currentLoadDir}/Textures/Wardrobe/{indir}" );
            CollectTextures( textures, $"./{indir}", withbasename,
                $"{SuperController.singleton.currentLoadDir}/Textures/{indir}" );

            // Add global texture files to the list
            CollectTextures( textures, $"/{indir}", withbasename,
                $"{SuperController.singleton.savesDir}../Textures/Wardrobe/{indir}" );
            CollectTextures( textures, $"/{indir}", withbasename, 
                $"{SuperController.singleton.savesDir}../Textures/{indir}" );

            if( textures.Count() == 0 )
            {
                SuperController.LogMessage( "Could not find a replacement texture at either the scene or global Vam directory" );
                SuperController.LogMessage( $"To replace this material, place a texture file named '{withbasename}.[png|jpg]' or `default.[png|jpg] in 'Textures/Wardrobe/{indir}'" );
            }

            return textures;
        }

        public void DumpButtonCallback()
        {
            OBJExporter exporter = new OBJExporter();
            DAZMesh mesh = myClothes.GetComponentsInChildren< DAZSkinWrap >()
                .First().dazMesh;

            exporter.Export( myClothes.name + ".obj", mesh.uvMappedMesh, mesh.uvMappedMesh.vertices, mesh.uvMappedMesh.normals, mesh.materials );
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
                    SuperController.LogMessage( "Detected Wardrobe version 1, re-save to update" );
                    ParseReplacementsV1( jc[ "replacements" ] as JSONClass );
                }
                else if( jc["version"].AsInt == 2 )
                {
                    SuperController.LogMessage( "Detected Wardrobe version 2, re-save to update" );
                    ParseReplacementsV2( jc[ "replacements" ] as JSONArray );
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
            
            public override bool StoreJSON( JSONClass jc, bool includePhysical = true, bool includeAppearance = true, bool forceStore = false )
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

                jc.Add( "version", new JSONData( 3 ) );
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
            
            private void ParseReplacementsV2( JSONArray replacements )
            {
                foreach( JSONClass obj in replacements )
                {
                    // Remove the skinwrap from the slot designator
                    string[] comps = obj["slot"].Value.Split( '/' );
                    string slot = $"{comps[0]}/{comps[2]}";

                    // Remove the extra subdirectory
                    entries[ new StorableSlot( slot, obj["shader"] ) ] =
                        TextureReference.fromReference( obj["texture"].Value );
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

        private static bool FileExists( string file )
        {
            string[] components = file.Split( '/', '\\' );
            string filename = components.Last();
            string directory = components.Take( components.Length - 1 )
                .Aggregate( (l, r) => l.Length > 0 && r.Length > 0 ? $"{l}/{r}" : $"{l}{r}" );
            
            try
            { 
                return 0 < SuperController.singleton.GetFilesAtPath( directory )
                    .Where( df => df.Split( '/', '\\' ).Last().ToLower() == filename.ToLower() )
                    .Count();
            }
            catch
            {
                // Directory probably doesn't exist
                return false;
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
                    var basename = reference.Split( '/', '\\' ).Last();
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

                string basedir;
                string basename;

                if( storedName.StartsWith( "./" ) )
                {
                    local = true;
                    basedir = $"{SuperController.singleton.currentLoadDir}/Textures";
                    basename = storedName.Remove( 0, 2 );
                }
                else
                {
                    local = false;
                    basedir = $"{SuperController.singleton.savesDir}/../Textures";
                    basename = storedName.Remove( 0, 1 );
                }

                filename = $"{basedir}/Wardrobe/{basename}";
                if( ! FileExists( filename ) )
                { 
                    filename = $"{basedir}/{basename}";
                    if( ! FileExists( filename ) )
                        SuperController.LogError( "Could not find texture reference " + storedName );
                }

                return new TextureReference( local, storedName, filename );
            }
        }

        private class ShaderRefControl : JSONStorableBool
        {
            public readonly string propName;
            public UIDynamicToggle ui;

            private bool masked;
            private bool wasEnabled;

            public ShaderRefControl( MVRScript parent, string displayName, string propName, bool startingValue )
                : base( displayName, startingValue )
            {
                this.propName = propName;
                this.wasEnabled = startingValue;
                ui = parent.CreateToggle( this, false );
            }

            public void MaskMaterial( Material material )
            {
                if( material.HasProperty( propName ) )
                { 
                    ClearMask();
                }
                else
                {

                    // Store our current value (if we're not already masked)
                    if( ! masked )
                        wasEnabled = val;

                    // Disable and uncheck ourselves.
                    ui.toggle.interactable = false;
                    val = false;
                    masked = true;
                }
            }

            public void ClearMask()
            {
                if( masked ) 
                    val = wasEnabled;

                ui.toggle.interactable = true;
                masked = false;
            }
        }

        private static List< string > EMPTY_CHOICES = new List< string >();
        private static readonly string PROP_DIFFUSE = "_MainTex";
        private static readonly string PROP_CUTOUT  = "_AlphaTex";
        private static readonly string PROP_NORMAL  = "_BumpMap";
        private static readonly string PROP_GLOSS   = "_GlossTex";
        private static readonly string PROP_SPEC    = "_SpecTex";
    }

}
