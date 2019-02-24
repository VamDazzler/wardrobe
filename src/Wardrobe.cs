using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/**
 * Outfit manager.
 *
 * Apply outfits to clothing pieces.
 *
 * Authors: VamDazzler
 * License: Creative Commons with Attribution (CC BY 3.0)
 */
namespace VamDazzler
{
    public class Wardrobe : MVRScript
    {
        private bool disableUpdate;

        //person script is attatched too
        JSONStorableStringChooser clothingItems, outfitNames;
        JSONStorableString materialList;
        UIDynamicButton applyButton, dumpButton;
        StorableReplacements storedOutfits;

        // Indicate whether loading from the JSON has completed.
        // Initial load of textures must wait until the clothes have all been loaded,
        // which is not the case by the time of `Start` on a fresh start of VaM.
        private bool needsLoad;

        private VDTextureLoader textureLoader = new VDTextureLoader();

        public override void Init()
        {
            try
            {
                disableUpdate = true;
                pluginLabelJSON.val = "Wardrobe v2.0.0 (by VamDazzler)";

                // Obtain our person
                if( containingAtom == null )
                {
                    SuperController.LogError("Please add this plugin to a PERSON atom.");
                    throw new Exception( "Halting Wardrobe due to de-Atom-ization" );
                }

                // Create the clothing items drop-down
                clothingItems = new JSONStorableStringChooser( "clothing", EMPTY_CHOICES, null, "Clothing Item" );
                UIDynamicPopup clothingSelector = CreateScrollablePopup( clothingItems );

                // Create the outfit selection drop-down
                outfitNames = new JSONStorableStringChooser( "outfit", EMPTY_CHOICES, null, "Outfit" );
                UIDynamicPopup outfitSelector = CreateScrollablePopup( outfitNames );
                outfitSelector.popupPanelHeight = 900f;
                RectTransform panel = outfitSelector.popup.popupPanel;
                panel.SetSizeWithCurrentAnchors( RectTransform.Axis.Horizontal, 400f );
                panel.pivot = new Vector2( 0.35f, 1.0f );

                // Create the slot in which all changed textures are stored.
                storedOutfits = new StorableReplacements();
                RegisterString( storedOutfits );

                // Action to perform replacement
                applyButton = CreateButton( "Apply" );
                applyButton.button.onClick.AddListener( ApplyOutfitCallback );
                
                // Create a dump button
                UIDynamic align = CreateSpacer( true );
                align.height = 25;
                dumpButton = CreateButton("Dump OBJ and MTL files - look in root", true);
                if (dumpButton != null)
                {
                    dumpButton.button.onClick.AddListener(DumpButtonCallback);
                    dumpButton.button.interactable = false;
                }

                // Create the material listing window
                materialList = new JSONStorableString( "matlist", "" );
                UIDynamicTextField matListTextField = CreateTextField( materialList, true );
                matListTextField.height = 400f;
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
                    foreach( var entry in storedOutfits.All() )
                    {
                        try
                        {
                            ApplyOutfit( entry.Value, entry.Key );
                        }
                        catch( Exception ex )
                        {
                            SuperController.LogError( $"Could not load outfit '{entry.Value}' for {entry.Key}: {ex}" );
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
                if( containingAtom == null )
                    return;

                // Now that loading is complete, set our UI callbacks
                clothingItems.setCallbackFunction = this.SelectClothingItem;
                outfitNames.setCallbackFunction = this.SelectOutfit;
                SelectClothingItem( null );

                needsLoad = true;
            }
            catch( Exception ex )
            {
                SuperController.LogError( $"Could not start Wardrobe {ex}" );
                disableUpdate = true;
            }
        }

        private IEnumerable< string > FindOutfits( string forClothing )
        {
            string localDirectory = $"{SuperController.singleton.currentLoadDir}/Textures/Wardrobe/{forClothing}";
            string globalDirectory = $"{SuperController.singleton.savesDir}/../Textures/Wardrobe/{forClothing}";

            // Collect outfit directories from both the scene and global levels.
            return safeGetDirectories( localDirectory ).Union( safeGetDirectories( globalDirectory ) )
                .Select( getBaseName )
                .Where( bn => bn.ToLower() != "psd" )
                .Distinct( StringComparer.OrdinalIgnoreCase );
        }
        
        //
        // UI action callbacks

        private void SelectClothingItem( string clothingName )
        {
            SelectOutfit( null );
            if( clothingName == null )
            {
                List< string > clothings = GameObject
                    .FindObjectsOfType< DAZClothingItem >()
                    .Where( dci => dci.containingAtom == containingAtom )
                    .Select( dci => dci.name )
                    .ToList();
                clothings.Insert( 0, "REFRESH" );
                clothingItems.choices = clothings;

                // No clothing selected, disable dumping OBJs.
                dumpButton.button.interactable = false;

                // Update the material list to show nothing
                materialList.val = "(material list, select clothes)";
            }
            else if( clothingName == "REFRESH" )
            {
                // call us again with no value.
                clothingItems.val = null;
            }
            else
            {
                // Turn on the OBJ dump
                dumpButton.button.interactable = true;
                
                // Create the list of materials.
                string matlist = GameObject
                    .FindObjectsOfType< DAZClothingItem >()
                    .Where( dci => dci.containingAtom == containingAtom )
                    .Where( dci => dci.name == clothingName )
                    .First()
                    .GetComponentsInChildren< DAZSkinWrap >()
                    .First()
                    .GPUmaterials
                    .Select( mat => mat.name )
                    .Aggregate( (l,r) => l.Length > 0 && r.Length > 0 ? $"{l}\n{r}" : $"{l}{r}" );
                materialList.val = matlist;

                // Get a list of outfits
                List< string > outfits = FindOutfits( clothingName ).ToList();
                outfitNames.choices = outfits;

                if( outfits.Count == 1 )
                {
                    // Pre-select the single outfit.
                    outfitNames.val = outfits.ElementAt( 0 );
                }
            }
        }

        private void SelectOutfit( string outfitName )
        {
            if( outfitName == null )
            {
                outfitNames.choices = EMPTY_CHOICES;
                outfitNames.valNoCallback = null;
                applyButton.button.interactable = false;
            }
            else
            {
                applyButton.button.interactable = true;
            }
        }

        public void DumpButtonCallback()
        {
            // Obtain the currently selected clothes.
            DAZClothingItem clothes = GameObject
                .FindObjectsOfType< DAZClothingItem >()
                .Where( dci => dci.containingAtom == containingAtom )
                .Where( dci => dci.name == clothingItems.val )
                .DefaultIfEmpty( (DAZClothingItem) null )
                .FirstOrDefault();

            // Bug out if it doesn't exist.
            if( clothes == null )
            { 
                SuperController.LogError( $"Could not finding clothing item '{clothingItems.val}'" );
                return;
            }

            // Get the first skinwrap mesh.
            OBJExporter exporter = new OBJExporter();
            DAZMesh mesh = clothes
                .GetComponentsInChildren< DAZSkinWrap >()
                .First().dazMesh;

            // Export
            exporter.Export( clothes.name + ".obj", mesh.uvMappedMesh, mesh.uvMappedMesh.vertices, mesh.uvMappedMesh.normals, mesh.materials );
        }
        
        private void ApplyOutfitCallback()
        {
            try
            { 
                if( clothingItems.val != null && outfitNames.val != null )
                    ApplyOutfit( outfitNames.val, clothingItems.val );
                storedOutfits.setOutfit( clothingItems.val, outfitNames.val );
            }
            catch( Exception ex )
            {
                SuperController.LogError( "Could not apply outfit: " + ex );
            }
        }

        private void ApplyOutfit( string outfitName, string forClothing )
        {
            string sceneDirectory = $"{SuperController.singleton.currentLoadDir}/Textures/Wardrobe/{forClothing}";
            string globalDirectory = $"{SuperController.singleton.savesDir}/../Textures/Wardrobe/{forClothing}";

            string outfitDirectory = safeGetDirectories( sceneDirectory )
                .Union( safeGetDirectories( globalDirectory ) )
                .Where( dir => getBaseName( dir ).ToLower() == outfitName.ToLower() )
                .DefaultIfEmpty( (string) null )
                .FirstOrDefault();

            if( outfitDirectory == null )
            {
                SuperController.LogError( $"Outfit needs textures in '<vamOrScene>/Textures/Wardrobe/{forClothing}/{outfitName}'" );
                return;
            }

            // Get the clothing item materials.
            DAZClothingItem clothes = GameObject
                .FindObjectsOfType< DAZClothingItem >()
                .Where( dci => dci.containingAtom == containingAtom )
                .Where( dci => dci.name == forClothing )
                .FirstOrDefault();
            if( clothes == null )
                throw new Exception( "Tried to apply '{outfitName}' to '{forClothing}' but '{myPerson.name}' isn't wearing that." );
            
            string[] files = SuperController.singleton.GetFilesAtPath( outfitDirectory );
            
            foreach( Material mat in clothes
                .GetComponentsInChildren< DAZSkinWrap >()
                .SelectMany( dsw => dsw.GPUmaterials ) )
            {
                ApplyTexture( outfitDirectory, mat, PROP_DIFFUSE );
                ApplyTexture( outfitDirectory, mat, PROP_CUTOUT );
                ApplyTexture( outfitDirectory, mat, PROP_NORMAL );
                ApplyTexture( outfitDirectory, mat, PROP_SPEC );
                ApplyTexture( outfitDirectory, mat, PROP_GLOSS );
            }
        }

        //
        // Outfit application methods

        private void ApplyTexture( string outfitDirectory, Material mat, string property )
        {
            string textureFilename = texNames( mat, property )
                .SelectMany( tn => SuperController.singleton.GetFilesAtPath( outfitDirectory, $"{tn}.*" ) )
                .DefaultIfEmpty( (string) null )
                .FirstOrDefault();

            if( textureFilename != null )
                textureLoader.withTexture( textureFilename, tex => mat.SetTexture( property, tex ) );
        }
        
        private static IEnumerable< string > diffuseTexNames( Material mat )
        {
            if( mat.HasProperty( PROP_DIFFUSE ) )
            { 
                bool hasAlpha = mat.HasProperty( PROP_CUTOUT );

                yield return $"{mat.name}D";
                if( hasAlpha ) yield return $"{mat.name}";
                yield return "defaultD";
                if( hasAlpha ) yield return "default";
            }
        }

        private static IEnumerable< string > alphaTexNames( Material mat )
        {
            if( mat.HasProperty( PROP_CUTOUT ) )
            { 
                bool hasDiffuse = mat.HasProperty( PROP_DIFFUSE );

                yield return $"{mat.name}A";
                if( hasDiffuse ) yield return $"{mat.name}";
                yield return $"defaultA";
                if( hasDiffuse ) yield return $"default";
            }
        }

        private static IEnumerable< string > otherTexNames( Material mat, string propName, string suffix )
        {
            if( mat.HasProperty( propName ) )
            {
                yield return $"{mat.name}{suffix}";
                yield return $"default{suffix}";
            }
        }

        private static IEnumerable< string > texNames( Material mat, string propName )
        {
            switch( propName )
            {
                case PROP_DIFFUSE:
                    return diffuseTexNames( mat );
                case PROP_CUTOUT:
                    return alphaTexNames( mat );
                case PROP_GLOSS:
                    return otherTexNames( mat, PROP_GLOSS, "G" );
                case PROP_NORMAL:
                    return otherTexNames( mat, PROP_NORMAL, "N" );
                case PROP_SPEC:
                    return otherTexNames( mat, PROP_SPEC, "S" );

                default:
                    throw new Exception( $"Unknown shader property '{propName}'" );
            }
        }

        //
        // Helper classes and utility methods
        
        private class StorableReplacements : JSONStorableString
        {
            private Dictionary< string, string > entries = new Dictionary< string, string >();

            public StorableReplacements() : base( "replacements", "<placeholder>" )
            {
            }

            public void setOutfit( string clothingName, string outfitName )
            {
                entries[ clothingName ] = outfitName;
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
                entries = new Dictionary< string, string >();
                if( ! jc.Keys.Contains( "version" ) || jc["version"].AsInt != 4 )
                { 
                    // this is version 1, the undocumented
                    SuperController.LogError( "Cannot load Wardrobe v1 save. Everything has changed, sorry." );
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
                    entries[ obj["clothes" ] ] = obj[ "outfit" ];
                }
            }
            
            public override bool StoreJSON( JSONClass jc, bool includePhysical = true, bool includeAppearance = true, bool forceStore = false )
            {
                var replacements = new JSONArray();
                foreach( var kvp in entries )
                {
                    JSONClass obj = new JSONClass();
                    obj["clothes"] = kvp.Key;
                    obj["outfit"] = kvp.Value;
                    replacements.Add( obj );
                }

                jc.Add( "version", new JSONData( 4 ) );
                jc.Add( "replacements", replacements );
                return true;
            }
        }

        private static string[] safeGetDirectories( string inDir )
        {
            try
            {
                return SuperController.singleton.GetDirectoriesAtPath( inDir );
            }
            catch
            {
                return new string[0];
            }
        }
        
        // Get the basename (last part of a path, usually filename) from a fully qualified filename.
        private static string getBaseName( string fqfn )
        {
            string[] comps = fqfn.Split( '\\', '/' );
            return comps[ comps.Length - 1 ];
        }

        private static string removeExt( string fn )
        {
            return fn.Substring( 0, fn.LastIndexOf( '.' ) );
        }

        private static string onlyExt( string fn )
        {
            return fn.Substring( fn.LastIndexOf( '.' ) + 1 );
        }

        private static readonly List< string > EMPTY_CHOICES = new List< string >();
        private const string PROP_DIFFUSE = "_MainTex";
        private const string PROP_CUTOUT  = "_AlphaTex";
        private const string PROP_NORMAL  = "_BumpMap";
        private const string PROP_GLOSS   = "_GlossTex";
        private const string PROP_SPEC    = "_SpecTex";
    }

}
