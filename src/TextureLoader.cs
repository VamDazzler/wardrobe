using System.Collections.Generic;
using UnityEngine;

namespace VamDazzler
{
    public class VDTextureLoader
    {
        public const int TYPE_DIFFUSE = 0;
        public const int TYPE_NORMAL  = 1;
        public const int TYPE_SPECULAR = 2;
        public const int TYPE_GLOSS    = 3;

        public delegate void TextureCallback( Texture2D t2d );

        private Dictionary< string, TextureState > textureCache = new Dictionary< string, TextureState >();

        /**
         * Load (or reuse) a texture from a file to perform an action.
         */
        public void withTexture( string textureFile, int textureType, TextureCallback action )
        {
            if( textureCache.ContainsKey( textureFile ) )
            {
                textureCache[ textureFile ].withTexture( action );
            }
            else
            {
                // Create the texture state object
                TextureState newState = new TextureState();
                newState.withTexture( action );
                textureCache[ textureFile ] = newState;

                bool createMipMaps = true;
                bool linear = false;
                bool isNormal = false;
                bool compress = true;

                switch( textureType )
                {
                    case TYPE_SPECULAR:
                    case TYPE_GLOSS:
                        linear = true;
                        break;

                    case TYPE_NORMAL:
                        linear = true;
                        isNormal = true;
                        compress = false;
                        break;

                    default:
                        break;
                }

                // Begin loading the texture
                var img = new ImageLoaderThreaded.QueuedImage();
                img.imgPath = textureFile;
                img.callback = qimg => newState.applyTexture( qimg );
                img.createMipMaps = createMipMaps;
                img.isNormalMap = isNormal;
                img.linear = linear;
                img.compress = compress;

                ImageLoaderThreaded.singleton.QueueImage( img );
            }
        }

        /**
         * Expire (remove) textures from the cache so that they can be reloaded.
         */
        public void ExpireDirectory( string directory )
        {
            List< string > files = new List<string>();
            foreach( KeyValuePair< string, TextureState > file in textureCache )
            {
                if( file.Key.StartsWith( directory ) )
                    files.Add( file.Key );
            }

            files.ForEach( f => textureCache.Remove( f ) );
        }

        // A simple class to maintain the state of, and act on, loaded textures
        private class TextureState
        {
            private Texture2D loadedTexture;

            private TextureCallback loadingCallback = BLANK;

            /* Take an action if/when the texture is loaded.
             */
            public void withTexture( TextureCallback callback )
            {
                if( loadedTexture != null )
                {
                    callback.Invoke( loadedTexture );
                }
                else
                {
                    TextureCallback prev = loadingCallback;
                    loadingCallback = (t2d) => { prev.Invoke( t2d ); callback.Invoke( t2d ); };
                }
            }

            public void applyTexture( ImageLoaderThreaded.QueuedImage tex )
            {
                if( tex.hadError )
                {
                    SuperController.LogError( "Error loading texture: " + tex.errorText );
                }
                else
                { 
                    loadedTexture = tex.tex;
                    loadingCallback.Invoke( tex.tex );
                    loadingCallback = BLANK;
                }
            }

            private static void BLANK( Texture2D t2d )
            {
                // This space intentionally blank
            }
        }
    }
}