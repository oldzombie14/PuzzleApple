// Minimal unseeded 2D adaptation of psrdnoise, read from:
// https://github.com/chmodseven/psrdnoise/blob/main/Assets/Shaders/HLSL/psrdnoise2.hlsl
// Original authors Stefan Gustavson and Ian McEwan; HLSL port Shannon Rowe.
// Periodic wrapping and optional seed machinery omitted for this local study.
// Copyright (c) 2021 Stefan Gustavson and Ian McEwan.
// Copyright (c) 2022 Shannon Rowe (HLSL port).
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
#ifndef PUZZLEAPPLE_STUDY_FLOW_NOISE
#define PUZZLEAPPLE_STUDY_FLOW_NOISE
float3 StudyMod289(float3 x){return x-floor(x/289.0)*289.0;}
float StudyFlowNoise(float2 pos,float alpha,out float2 gradient)
{
    float2 uv=float2(pos.x+pos.y*.5,pos.y),i0=floor(uv),f0=frac(uv);
    float cmp=step(f0.y,f0.x);float2 o1=float2(cmp,1-cmp);
    float2 i1=i0+o1,i2=i0+1;
    float2 v0=float2(i0.x-i0.y*.5,i0.y),v1=v0+float2(o1.x-o1.y*.5,o1.y),v2=v0+float2(.5,1);
    float2 x0=pos-v0,x1=pos-v1,x2=pos-v2;
    float3 iu=float3(i0.x,i1.x,i2.x),iv=float3(i0.y,i1.y,i2.y);
    float3 hash=StudyMod289(iu);
    hash=StudyMod289((hash*51+2)*hash+StudyMod289(iv));
    hash=StudyMod289((hash*34+10)*hash);
    float3 psi=hash*.07482+alpha,gx=cos(psi),gy=sin(psi);
    float2 g0=float2(gx.x,gy.x),g1=float2(gx.y,gy.y),g2=float2(gx.z,gy.z);
    float3 w=max(.8-float3(dot(x0,x0),dot(x1,x1),dot(x2,x2)),0),w2=w*w,w4=w2*w2;
    float3 gdotx=float3(dot(g0,x0),dot(g1,x1),dot(g2,x2));
    float3 dw=w2*w*(-8)*gdotx;
    gradient=(g0*w4.x+x0*dw.x+g1*w4.y+x1*dw.y+g2*w4.z+x2*dw.z)*10.9;
    return dot(w4,gdotx)*10.9;
}
#endif
