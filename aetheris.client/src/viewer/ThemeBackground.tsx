import { useFrame, useThree } from "@react-three/fiber";
import { useEffect, useMemo } from "react";
import { ShaderMaterial, Vector2 } from "three";
import type { ViewportTheme } from "./viewportTheme";

const vertexShader = /* glsl */ `
	void main() {
		gl_Position = vec4(position.xy, 1.0, 1.0);
	}
`;

const fragmentShader = /* glsl */ `
	precision highp float;
	uniform vec2 uResolution;
	uniform float uMode;
	uniform float uIntensity;
	uniform float uVignette;
	uniform float uBloom;

	float hash21(vec2 p) {
		p = fract(p * vec2(123.34, 456.21));
		p += dot(p, p + 45.32);
		return fract(p.x * p.y);
	}

	float noise21(vec2 p) {
		vec2 i = floor(p);
		vec2 f = fract(p);
		f = f * f * (3.0 - 2.0 * f);
		return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
			mix(hash21(i + vec2(0.0, 1.0)), hash21(i + 1.0), f.x), f.y);
	}

	float stars(vec2 uv, float density) {
		vec2 cell = floor(uv * uResolution / 2.5);
		float h = hash21(cell);
		float spark = smoothstep(1.0 - density, 1.0, h);
		return spark * (0.35 + 0.65 * hash21(cell + 17.1));
	}

	float ring(vec2 p, float radius, float width) {
		return exp(-abs(length(p) - radius) / width);
	}

	vec3 mars(vec2 uv, vec2 p) {
		float horizon = smoothstep(-0.42, 0.2, p.y);
		vec3 color = mix(vec3(0.045, 0.012, 0.009), vec3(0.31, 0.075, 0.035), horizon);
		color += vec3(0.36, 0.09, 0.025) * exp(-abs(p.y + 0.18) * 9.0);
		vec2 sunP = p - vec2(-0.58, 0.28);
		float sun = 1.0 - smoothstep(0.055, 0.07, length(sunP));
		float halo = exp(-length(sunP) * 5.0);
		color += vec3(1.0, 0.37, 0.10) * (sun * 1.5 + halo * 0.38);
		float strataNoise = noise21(vec2(p.x * 2.1, p.y * 4.0));
		float strata = pow(0.5 + 0.5 * sin((p.y + strataNoise * 0.07) * 95.0), 14.0);
		color += vec3(0.42, 0.11, 0.035) * strata * smoothstep(0.15, -0.75, p.y) * 0.32;
		float dust = stars(uv * vec2(1.0, 0.75), 0.0028);
		color += vec3(0.8, 0.3, 0.12) * dust * 0.32;
		return color;
	}

	vec3 sirius(vec2 uv, vec2 p) {
		vec3 color = mix(vec3(0.001, 0.004, 0.018), vec3(0.006, 0.028, 0.075), uv.y);
		color += vec3(0.35, 0.62, 1.0) * stars(uv, 0.0035);
		vec2 q = p - vec2(0.62, 0.34);
		float r = length(q);
		float core = exp(-r * 38.0);
		float halo = exp(-r * 7.0);
		float rays = exp(-abs(q.x) * 70.0) + exp(-abs(q.y) * 90.0);
		rays += exp(-abs(q.x + q.y) * 100.0) * 0.28 + exp(-abs(q.x - q.y) * 100.0) * 0.28;
		color += vec3(0.58, 0.78, 1.0) * halo * (0.35 + uBloom);
		color += vec3(0.9, 0.97, 1.0) * (core * 2.4 + rays * exp(-r * 5.0) * 0.25);
		float orbit = ring(q * vec2(1.0, 1.55), 0.34, 0.0025);
		color += vec3(0.2, 0.5, 0.9) * orbit * 0.22;
		return color;
	}

	vec3 singularity(vec2 uv, vec2 p) {
		// Deliberately off-axis so the engineering silhouette never disappears into the event horizon.
		vec2 q = p - vec2(1.02, 0.08);
		q.y *= 1.65;
		float r = length(q);
		float a = atan(q.y, q.x);
		vec3 color = vec3(0.0005, 0.0002, 0.0015);
		float warped = pow(0.5 + 0.5 * sin(log(r + 0.018) * 34.0 + a * 3.0), 10.0);
		color += mix(vec3(0.25, 0.018, 0.18), vec3(1.0, 0.35, 0.055), smoothstep(-0.6, 0.8, sin(a)))
			* warped * smoothstep(0.72, 0.11, r) * 0.52;
		float accretion = exp(-abs(r - 0.205) * 62.0);
		float hot = exp(-abs(r - 0.205) * 155.0);
		color += vec3(0.95, 0.18, 0.045) * accretion * (0.55 + uBloom);
		color += vec3(1.0, 0.82, 0.45) * hot * 1.25;
		float lens = ring(q, 0.31, 0.004) + ring(q, 0.42, 0.003) * 0.35;
		color += vec3(0.45, 0.18, 0.7) * lens * 0.35;
		color *= smoothstep(0.145, 0.19, r);
		return color;
	}

	vec3 aeons(vec2 uv, vec2 p) {
		vec3 color = mix(vec3(0.006, 0.003, 0.018), vec3(0.035, 0.015, 0.065), uv.y);
		color += vec3(0.45, 0.36, 0.16) * stars(uv, 0.0024) * 0.62;
		vec2 q = p - vec2(-0.52, -0.38);
		float r = length(q);
		float haze = exp(-abs(r - 0.78) * 5.0);
		color += vec3(0.18, 0.09, 0.28) * haze * 0.25;
		float arcs = ring(q, 0.69, 0.0025) + ring(q, 0.83, 0.003) * 0.65 + ring(q, 1.04, 0.002) * 0.45;
		color += vec3(0.68, 0.48, 0.17) * arcs * 0.48;
		vec2 g = p - vec2(0.58, 0.24);
		float spokes = pow(abs(sin(atan(g.y, g.x) * 9.0)), 70.0) * smoothstep(0.62, 0.08, length(g));
		color += vec3(0.38, 0.27, 0.11) * spokes * 0.18;
		color += vec3(0.16, 0.08, 0.26) * exp(-length(p - vec2(0.0, 0.35)) * 2.2) * 0.3;
		return color;
	}

	void main() {
		vec2 uv = gl_FragCoord.xy / max(uResolution, vec2(1.0));
		vec2 p = uv * 2.0 - 1.0;
		p.x *= uResolution.x / max(uResolution.y, 1.0);
		vec3 color = vec3(0.0);
		if (uMode < 1.5) color = mars(uv, p);
		else if (uMode < 2.5) color = sirius(uv, p);
		else if (uMode < 3.5) color = singularity(uv, p);
		else color = aeons(uv, p);
		float edge = smoothstep(1.35, 0.25, length(uv - 0.5));
		color *= mix(1.0, edge, uVignette);
		gl_FragColor = vec4(color * uIntensity, 1.0);
	}
`;

const shaderMode: Record<Exclude<ViewportTheme["background"]["kind"], "flat">, number> = {
	mars: 1,
	sirius: 2,
	singularity: 3,
	aeons: 4,
};

export function ThemeBackground({ theme }: { theme: ViewportTheme }) {
	const { size } = useThree();
	const kind = theme.background.kind;
	const material = useMemo(() => {
		if (kind === "flat") return null;
		return new ShaderMaterial({
			vertexShader,
			fragmentShader,
			depthTest: false,
			depthWrite: false,
			uniforms: {
				uResolution: { value: new Vector2(1, 1) },
				uMode: { value: shaderMode[kind] },
				uIntensity: { value: theme.background.intensity },
				uVignette: { value: theme.postProcess.vignette },
				uBloom: { value: theme.postProcess.bloom },
			},
		});
	}, [kind, theme.background.intensity, theme.postProcess.bloom, theme.postProcess.vignette]);

	useEffect(() => () => material?.dispose(), [material]);
	useFrame(({ gl }) => {
		material?.uniforms.uResolution.value.set(
			size.width * gl.getPixelRatio(),
			size.height * gl.getPixelRatio(),
		);
	});
	if (!material) return null;
	return (
		<mesh material={material} renderOrder={-1000} frustumCulled={false} raycast={() => null}>
			<planeGeometry args={[2, 2]} />
		</mesh>
	);
}
