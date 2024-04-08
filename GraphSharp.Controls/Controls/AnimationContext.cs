namespace GraphSharp.Controls
{
    public class AnimationContext : IAnimationContext
    {
        public AnimationContext(GraphCanvas canvas)
        {
            GraphCanvas = canvas;
        }

        public GraphCanvas GraphCanvas { get; private set; }
    }
}