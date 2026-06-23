using System;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// Texture2D 를 JPEG/PNG 로 인코딩하고 선택적으로 리사이즈하는 공유 유틸리티.
    /// TakeScreenshot / TakeSceneViewScreenshot / RenderPrefabPreview 핸들러가 공유한다.
    /// </summary>
    public static class ImageEncoder
    {
        /// <summary>
        /// 텍스처를 base64 이미지로 인코딩한다.
        /// </summary>
        /// <param name="tex">인코딩할 텍스처. 이 메서드는 입력 텍스처를 파괴하지 않는다(파괴 책임은 호출자).</param>
        /// <param name="quality">JPEG 품질(1-100). 0이면 PNG 로 인코딩.</param>
        /// <param name="maxWidth">0보다 크고 tex.width 보다 작으면 비율을 유지하며 축소.</param>
        public static EncodedImage Encode(Texture2D tex, int quality, int maxWidth)
        {
            if (tex == null)
                throw new ArgumentNullException(nameof(tex));

            Texture2D working = tex;
            bool ownsWorking = false;
            try
            {
                if (maxWidth > 0 && working.width > maxWidth)
                {
                    working = Resize(working, maxWidth);
                    ownsWorking = true;
                }

                byte[] bytes = quality > 0 ? working.EncodeToJPG(quality) : working.EncodeToPNG();
                return new EncodedImage
                {
                    imageBase64 = Convert.ToBase64String(bytes),
                    mimeType = quality > 0 ? "image/jpeg" : "image/png",
                    width = working.width,
                    height = working.height
                };
            }
            finally
            {
                // 내부에서 만든 임시본만 정리. 원본 tex 는 호출자가 파괴한다.
                if (ownsWorking) UnityEngine.Object.DestroyImmediate(working);
            }
        }

        private static Texture2D Resize(Texture2D src, int maxWidth)
        {
            float ratio = (float)maxWidth / src.width;
            int newHeight = Mathf.RoundToInt(src.height * ratio);

            var resized = new Texture2D(maxWidth, newHeight);
            var rt = RenderTexture.GetTemporary(maxWidth, newHeight);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                resized.ReadPixels(new Rect(0, 0, maxWidth, newHeight), 0, 0);
                resized.Apply();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            return resized;
        }
    }

    /// <summary>
    /// 인코딩 결과. 필드명은 Bridge 측 파싱 계약(imageBase64/mimeType/width/height)과 일치한다.
    /// </summary>
    public sealed class EncodedImage
    {
        public string imageBase64;
        public string mimeType;
        public int width;
        public int height;
    }
}
