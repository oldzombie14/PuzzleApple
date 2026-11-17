#ifndef PUZZLE_APPLE_SENTENCE_GLOW
#define PUZZLE_APPLE_SENTENCE_GLOW
float4x4 _P5JourneyInverse;
float4 _P5SpaceEnds, _P5GardenPlane, _P5VitruvianCentre, _P5VitruvianExtents, _P5RoomSentenceGlow[9];
float _P5ChapterMode;
int SentenceSpace(float3 world)
{
    if (_P5ChapterMode > 1.5 && all(abs(world-_P5VitruvianCentre.xyz)<=_P5VitruvianExtents.xyz)) return 8;
    if (_P5ChapterMode > 1.5) return dot(float4(world,1),_P5GardenPlane)<0 ? 7 : 6;
    if (_P5ChapterMode > .5) return 5;
    float z = mul(_P5JourneyInverse,float4(world,1)).z;
    return z < _P5SpaceEnds.x ? 0 : z < _P5SpaceEnds.y ? 1 : z < _P5SpaceEnds.z ? 2 : z < _P5SpaceEnds.w ? 3 : 4;
}
float FloorSentenceGlow(float3 world) { return .85*smoothstep(0,1,_P5RoomSentenceGlow[SentenceSpace(world)].z); }
#endif
