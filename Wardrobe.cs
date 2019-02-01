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
        JSONStorableStringChooser clothingItems, skinWraps, materials, textures;
        UIDynamicButton applyButton, dumpButton;
        List< ShaderRef > supportedShaderProperties;
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
                supportedShaderProperties = new List< ShaderRef >();
                supportedShaderProperties.Add( new ShaderRef( this, "Diffuse texture", PROP_DIFFUSE, true ) );
                supportedShaderProperties.Add( new ShaderRef( this, "Cutout", PROP_CUTOUT, true ) );
                supportedShaderProperties.Add( new ShaderRef( this, "Specular map", "_SpecTex", false ) );
                supportedShaderProperties.Add( new ShaderRef( this, "Glossy", PROP_GLOSS, false ) );
                supportedShaderProperties.Add( new ShaderRef( this, "Normal map", PROP_NORMAL, false ) );

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
                /* Unfortunately, these give the parameters controllable in the UI already * /
                Array.ForEach( myClothes.GetComponentsInChildren< DAZSkinWrapMaterialOptions >(),
                    a => { 
                        SuperController.LogMessage( "Examining skin wrap: " + a.name );
                        string[] slots = { 
                            a.param1Name, a.param2Name, a.param3Name, a.param4Name, a.param5Name,
                            a.param6Name, a.param7Name, a.param8Name, a.param9Name, a.param10Name };
                        Array.ForEach( slots, sn => SuperController.LogMessage( $"  Param Name: {sn}" ) );
                    } );
                // */
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

                /* TODO: Still trying to find more shader properties names * /
                int maintex = Shader.PropertyToID( PROP_DIFFUSE );
                int bumptex = Shader.PropertyToID( PROP_BUMP );
                int cutttex = Shader.PropertyToID( PROP_CUTOUT );
                int glostex = Shader.PropertyToID( PROP_GLOSS );
                int test    = Shader.PropertyToID( "_SpecTex" );

                Array.ForEach( mySkin.GPUmaterials, m =>
                {
                    SuperController.LogMessage( $"Querying material '{m.name}'" );
                    for( int i = 0; i < 1000; ++i )
                    {
                        try
                        {
                            Texture t = m.GetTexture( i );
                            if( t != null )
                            {
                                try
                                {
                                    string actual;
                                    if     ( i == maintex ) actual = PROP_DIFFUSE;
                                    else if( i == bumptex ) actual = PROP_BUMP;
                                    else if( i == cutttex ) actual = PROP_CUTOUT;
                                    else if( i == glostex ) actual = PROP_GLOSS;
                                    else if( i == test    ) actual = "<test>";
                                    else                    actual = "<unknown>";

                                    SuperController.LogMessage( $"  Texture #{i} is {actual}" );
                                    Array.ForEach( m.shaderKeywords, kw => 
                                        SuperController.LogMessage( $"    keyword: {kw}" ) );
                                }
                                catch( Exception ex )
                                {
                                    SuperController.LogMessage( $"  Could not cast it:) {ex}" );
                                }
                            }
                        }
                        catch
                        { 
                            // not a texture
                        }
                    }
                });
                // */

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

                // Now mask the available texture slots.
                supportedShaderProperties.ForEach( ssp => ssp.MaskMaterial( myMaterial ) );
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

        private class ShaderRef : JSONStorableBool
        {
            public readonly string propName;
            public UIDynamicToggle ui;

            private bool masked;
            private bool wasEnabled;

            public ShaderRef( MVRScript parent, string displayName, string propName, bool startingValue )
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
