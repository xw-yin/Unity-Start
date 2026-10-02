using UnityEngine;
namespace Homecoming.Editor
{
    public static class HomecomingBootRefinementReview
    {
        public static void Run()
        {
            HomecomingBootReview.Run();
            HomecomingMotionReview.Run();
            HomecomingMotionReview.RenderPreview();
            Debug.Log("REFINED BOOT MOTION AND PREVIEW PASSED");
        }
    }
}
