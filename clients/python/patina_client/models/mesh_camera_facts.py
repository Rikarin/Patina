from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="MeshCameraFacts")



@_attrs_define
class MeshCameraFacts:
    """ A camera a mesh file holds, as a client may look through it.

        Attributes:
            name (str): What the file calls it.
            position (list[float]): Where it stands, in the file's units.
            forward (list[float]): The unit direction it looks in.
            up (list[float]): The unit direction that is up in its picture, square to Forward.
            field_of_view (float): The whole vertical angle it sees, in radians; zero for an orthographic camera.
            aspect_ratio (float): Its picture's width over its height, or zero when the file does not say.
            orthographic_height (float): How tall the slab an orthographic camera sees is, in the file's units; zero for a
                camera with perspective.
     """

    name: str
    position: list[float]
    forward: list[float]
    up: list[float]
    field_of_view: float
    aspect_ratio: float
    orthographic_height: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        position = self.position



        forward = self.forward



        up = self.up



        field_of_view = self.field_of_view

        aspect_ratio = self.aspect_ratio

        orthographic_height = self.orthographic_height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "position": position,
            "forward": forward,
            "up": up,
            "fieldOfView": field_of_view,
            "aspectRatio": aspect_ratio,
            "orthographicHeight": orthographic_height,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        position = cast(list[float], d.pop("position"))


        forward = cast(list[float], d.pop("forward"))


        up = cast(list[float], d.pop("up"))


        field_of_view = d.pop("fieldOfView")

        aspect_ratio = d.pop("aspectRatio")

        orthographic_height = d.pop("orthographicHeight")

        mesh_camera_facts = cls(
            name=name,
            position=position,
            forward=forward,
            up=up,
            field_of_view=field_of_view,
            aspect_ratio=aspect_ratio,
            orthographic_height=orthographic_height,
        )


        mesh_camera_facts.additional_properties = d
        return mesh_camera_facts

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
