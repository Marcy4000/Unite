#ifndef TRAINER_COLOR_TONE_INCLUDED
#define TRAINER_COLOR_TONE_INCLUDED

float3 TrainerHairTone(float3 color)
{
    color = max(color, 0.0);
    float3 scaled = color * 0.45;
    return min(scaled * (color * 1.1295 + 0.03) /
        (scaled * (color * 1.0935 + 0.59) + 0.08), 100.0);
}

float3 TrainerEyeTone(float3 color)
{
    color = max(color, 0.0);
    float3 scaled = color * 0.45;
    float3 mapped = scaled / (scaled + 0.155) * 1.019;
    return min(pow(mapped, 2.2), 100.0);
}

#endif
