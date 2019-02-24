using System.Collections.Generic;
using UnityEngine;

namespace VamDazzler
{
    public class VDTextureLoader
    {
        public delegate void TextureCallback( Texture2D t2d );

        private Dictionary< string, TextureState > textureCache = new Dictionary< string, TextureState >();

        /**
         * Load (or reuse) a texture from a file to perform an action.
         */
        public void withTexture( string textureFile, TextureCallback action )
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

                // Begin loading the texture
                var img = new ImageLoaderThreaded.QueuedImage();
                img.imgPath = textureFile;
                img.callback = qimg => newState.applyTexture( qimg );
                ImageLoaderThreaded.singleton.QueueImage( img );
            }
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