from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportDisplay")



@_attrs_define
class ViewportDisplay:
    """ How the viewport is lit, shown and finished. None of it reaches a file that is exported; a project keeps it, so
    it opens as it was left. Angles are radians and exposure is in stops; an effect at zero is off. A field left out
    of a request is the one the viewport opens with.

        Attributes:
            environment (str): A built-in room (studio, overcast or sunset), or the path of an `.hdr` or `.exr` panorama.
                Default: 'studio'.
            environment_rotation (float): How far the environment has turned around the model. The light stays. Default:
                0.0.
            background_blur (float): How blurred the background is, 0 sharp to 1 a haze. Lighting is not blurred. Default:
                0.66.
            background_opacity (float): How much of the environment shows behind the model, 0 none (black) to 1 all.
                Default: 1.0.
            light_rotation (float): How far the light has turned around the model. Default: 0.0.
            exposure (float): Stops above or below the picture as it comes. Default: 0.0.
            shadows (float): How dark the model's shadow is, 0 none to 1 no direct light. Default: 1.0.
            tone_mapping (str): One of IReadOnlyList&lt;string&gt; ViewportToneMappings.All. Default: 'aces'.
            view (str): One of IReadOnlyList&lt;string&gt; ViewportViews.All. Default: 'lit'.
            wireframe (bool): Whether the triangles' edges are drawn over the model. Default: False.
            orthographic (bool): Whether parallel lines stay parallel. It keeps the size the picture has at the point the
                camera circles. Default: False.
            field_of_view (float): The vertical field of view, from 0.1 to 2.6. Default: 0.7.
            contrast (float): Contrast about middle grey, 0 to 2, where one leaves it alone. Default: 1.0.
            saturation (float): Colour, 0 grey to 2, where one leaves it alone. Default: 1.0.
            vignette (float): How dark the corners go, 0 to 1. Default: 0.0.
            grain (float): Film grain, 0 to 1. Default: 0.0.
            aberration (float): How far red and blue part towards the edges, 0 to 1. Default: 0.0.
            sharpen (float): Contrast-adaptive sharpening, 0 to 1. Default: 0.0.
            bloom (float): How much light spills past what the curve takes to white, 0 none to 1. Default: 0.0.
            warmth (float): Blue towards red, -1 to 1, where zero leaves the picture alone. A grade, not a temperature.
                Default: 0.0.
            tint (float): Green towards magenta, -1 to 1, where zero leaves the picture alone. Default: 0.0.
            background_color (str): What shows where the environment does not, as `#rrggbb` in sRGB: the background fades to
                it as
                BackgroundOpacity falls. It is lit by nothing and seen through the same curve as the model. Default: '#000000'.
            grid (bool): Whether a grid is drawn on the floor the model stands on, with the world's x and z axes. Default:
                False.
            picture_lane (str): Which lane of the picture shows: one of IReadOnlyList&lt;string&gt; ViewportLanes.All.
                Default: 'rgb'.
            picture_scale (float): What the picture's numbers are multiplied by before they are shown, 0.001 to 1000, so a
                height in the model's
                units or a position comes into view. Default: 1.0.
            shape (str): What a material or a graph with no mesh of its own is seen on: one of IReadOnlyList&lt;string&gt;
                ViewportShapes.All. A model
                that has a mesh is seen on its mesh whatever this says. Default: 'sphere'.
            look (None | str | Unset): A `.cube` table the frame is viewed through after the curve, or null.
            picture (None | str | Unset): The picture the `picture` view shows, on the Server's machine, or null.
     """

    environment: str = 'studio'
    environment_rotation: float = 0.0
    background_blur: float = 0.66
    background_opacity: float = 1.0
    light_rotation: float = 0.0
    exposure: float = 0.0
    shadows: float = 1.0
    tone_mapping: str = 'aces'
    view: str = 'lit'
    wireframe: bool = False
    orthographic: bool = False
    field_of_view: float = 0.7
    contrast: float = 1.0
    saturation: float = 1.0
    vignette: float = 0.0
    grain: float = 0.0
    aberration: float = 0.0
    sharpen: float = 0.0
    bloom: float = 0.0
    warmth: float = 0.0
    tint: float = 0.0
    background_color: str = '#000000'
    grid: bool = False
    picture_lane: str = 'rgb'
    picture_scale: float = 1.0
    shape: str = 'sphere'
    look: None | str | Unset = UNSET
    picture: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        environment = self.environment

        environment_rotation = self.environment_rotation

        background_blur = self.background_blur

        background_opacity = self.background_opacity

        light_rotation = self.light_rotation

        exposure = self.exposure

        shadows = self.shadows

        tone_mapping = self.tone_mapping

        view = self.view

        wireframe = self.wireframe

        orthographic = self.orthographic

        field_of_view = self.field_of_view

        contrast = self.contrast

        saturation = self.saturation

        vignette = self.vignette

        grain = self.grain

        aberration = self.aberration

        sharpen = self.sharpen

        bloom = self.bloom

        warmth = self.warmth

        tint = self.tint

        background_color = self.background_color

        grid = self.grid

        picture_lane = self.picture_lane

        picture_scale = self.picture_scale

        shape = self.shape

        look: None | str | Unset
        if isinstance(self.look, Unset):
            look = UNSET
        else:
            look = self.look

        picture: None | str | Unset
        if isinstance(self.picture, Unset):
            picture = UNSET
        else:
            picture = self.picture


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "environment": environment,
            "environmentRotation": environment_rotation,
            "backgroundBlur": background_blur,
            "backgroundOpacity": background_opacity,
            "lightRotation": light_rotation,
            "exposure": exposure,
            "shadows": shadows,
            "toneMapping": tone_mapping,
            "view": view,
            "wireframe": wireframe,
            "orthographic": orthographic,
            "fieldOfView": field_of_view,
            "contrast": contrast,
            "saturation": saturation,
            "vignette": vignette,
            "grain": grain,
            "aberration": aberration,
            "sharpen": sharpen,
            "bloom": bloom,
            "warmth": warmth,
            "tint": tint,
            "backgroundColor": background_color,
            "grid": grid,
            "pictureLane": picture_lane,
            "pictureScale": picture_scale,
            "shape": shape,
        })
        if look is not UNSET:
            field_dict["look"] = look
        if picture is not UNSET:
            field_dict["picture"] = picture

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        environment = d.pop("environment")

        environment_rotation = d.pop("environmentRotation")

        background_blur = d.pop("backgroundBlur")

        background_opacity = d.pop("backgroundOpacity")

        light_rotation = d.pop("lightRotation")

        exposure = d.pop("exposure")

        shadows = d.pop("shadows")

        tone_mapping = d.pop("toneMapping")

        view = d.pop("view")

        wireframe = d.pop("wireframe")

        orthographic = d.pop("orthographic")

        field_of_view = d.pop("fieldOfView")

        contrast = d.pop("contrast")

        saturation = d.pop("saturation")

        vignette = d.pop("vignette")

        grain = d.pop("grain")

        aberration = d.pop("aberration")

        sharpen = d.pop("sharpen")

        bloom = d.pop("bloom")

        warmth = d.pop("warmth")

        tint = d.pop("tint")

        background_color = d.pop("backgroundColor")

        grid = d.pop("grid")

        picture_lane = d.pop("pictureLane")

        picture_scale = d.pop("pictureScale")

        shape = d.pop("shape")

        def _parse_look(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        look = _parse_look(d.pop("look", UNSET))


        def _parse_picture(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        picture = _parse_picture(d.pop("picture", UNSET))


        viewport_display = cls(
            environment=environment,
            environment_rotation=environment_rotation,
            background_blur=background_blur,
            background_opacity=background_opacity,
            light_rotation=light_rotation,
            exposure=exposure,
            shadows=shadows,
            tone_mapping=tone_mapping,
            view=view,
            wireframe=wireframe,
            orthographic=orthographic,
            field_of_view=field_of_view,
            contrast=contrast,
            saturation=saturation,
            vignette=vignette,
            grain=grain,
            aberration=aberration,
            sharpen=sharpen,
            bloom=bloom,
            warmth=warmth,
            tint=tint,
            background_color=background_color,
            grid=grid,
            picture_lane=picture_lane,
            picture_scale=picture_scale,
            shape=shape,
            look=look,
            picture=picture,
        )


        viewport_display.additional_properties = d
        return viewport_display

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
