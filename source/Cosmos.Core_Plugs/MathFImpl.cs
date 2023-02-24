﻿using System;

namespace Cosmos.Core_Plugs
{
    // Temporary implementations, inneficent. Should use proper methods eventually.
    // See: https://linasm.sourceforge.net/docs/instructions/fpu.php
    [Plug(Target = typeof(MathF))]
    public static class MathFImpl
    {
        public static float Tan(float x) => (float)Math.Tan(x);
    }
}
