using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

//Nothing but proof of concept to load arbitrary clothing textures
namespace chokaphi
{
    public class ClothTexture : MVRScript
    {
        //person script is attatched too
        Atom parentAtom;

        public override void Init()
        {
            Setup();
        }
        protected void FixedUpdate() { }

        // Update is called with each rendered frame by Unity
        void Update() { }


        public void Setup()
        {
            if (containingAtom.mainController == null)
            {
                SuperController.LogError("Please add this plugin to a PERSON atom.");
                return;
            }
            //TODO:Use this to get the clothing information.
            parentAtom = containingAtom;
            SuperController.LogError(parentAtom.ToString());


            //Test Image to load
            LoadPNG(@"E:\VRGRILZ\VaM_Release1.14\01MissKringleDress.png");

            

        }

        //VAM does not give access to load texture so use VAM image loading routine instead
        //Wil call OnImageLoad once finished
        public void LoadPNG(string filePath)
        {

            ClothTexture.QueuedImage QI = new ClothTexture.QueuedImage();
            QI.imgPath = filePath;

    
            QI.callback = new ImageLoaderThreaded.ImageLoaderCallback(OnImageLoaded);
            //start image load
            ImageLoaderThreaded.singleton.QueueImage(QI);

        }

        private void OnImageLoaded(ImageLoaderThreaded.QueuedImage qi)
        {
            SuperController.LogError("IMAGE LOADED");
            //TODO: move out and only recheck if when cloting added and removed
            DAZClothingItem GO = GameObject.FindObjectOfType<DAZClothingItem>();
            DAZSkinWrap[] componentsInChildren = GO.GetComponentsInChildren<DAZSkinWrap>(true);
            foreach (DAZSkinWrap SW in componentsInChildren)
            {
                Material[] materials = SW.GPUmaterials;
                string[] materialNames = SW.materialNames;

                SuperController.LogError("materials found= " + materials.Length.ToString());


               foreach (Material M in materials)
                {
                    SuperController.LogError("Material Name ");
             
                    SuperController.LogError("mat name= " + M.name + " Main tex" + M.GetTexture("_MainTex").name);
                    //
                    //Set Main texture to the loaded Texture
                     M.SetTexture("_MainTex", qi.tex);
                    SuperController.LogError("updated " + M.name);
                }
            }
        }



        protected class QueuedImage : ImageLoaderThreaded.QueuedImage
        {
           
            //public DAZCharacterTextureControl.Region region;
           //public DAZCharacterTextureControl.TextureType textureType;
        }

        public ImageLoaderThreaded.ImageLoaderCallback callback;
        public string imgPath;
        
				
    }
}
