// Keep the part descending through the opening visible against the ground.
// Preserve its projected silhouette and its occlusion by objects above ground.
float4 GroundDepth(float4 cs,float3 ws)
{
    if(_PreserveMeshDepth>.5)return cs;
    float below=_PanelGroundY+.075-ws.y;
    if(below>0)
    {
        float3 view=GetWorldSpaceNormalizeViewDir(ws);
        if(view.y>.001)
        {
            // Compress the depth below grade into a thin layer. Its ordering
            // stays strictly monotonic, including across adjacent panel faces.
            float lift=.985*below;
            float3 contact=ws+view*min(lift/view.y,distance(ws,GetCameraPositionWS())*.99);
            float4 groundCS=TransformWorldToHClip(contact);
            cs.z=cs.w*groundCS.z/groundCS.w;
        }
    }
    return cs;
}
float CutDepth(float2 xz)
{
    float2 p=xz-_PanelSink.xy;
    float radius=length(p);
    float reach=max(_PanelSink.z,.001);
    float t=1-_PanelOpacity;
    float swell=.34*sin(dot(p,float2(.68,.29))+2*t)
               +.17*sin(dot(p,float2(-.35,.84))-1.4*t);
    float u=saturate(radius/reach);
    return radius + 4*u*(1-u)*swell - _PanelSink.w;
}
void ClipPanel(float2 xz)
{
    if(_PanelOpacity<1)clip(min(_PanelOpacity-.000001,-CutDepth(xz)));
}
