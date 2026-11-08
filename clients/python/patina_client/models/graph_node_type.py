from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_port_definition import GraphPortDefinition
  from ..models.graph_preset_state import GraphPresetState
  from ..models.graph_setting_definition import GraphSettingDefinition





T = TypeVar("T", bound="GraphNodeType")



@_attrs_define
class GraphNodeType:
    """ A node a client can add, for a palette.

        Attributes:
            path (str): What a document stores, such as `Colour/Levels`.
            title (str): The last segment of the path.
            category (str): The path without its title.
            summary (str): One line about it.
            has_preview (bool): Whether the type declares that its picture is worth showing.
            ports (list[GraphPortDefinition]): Its inputs and outputs.
            settings (list[GraphSettingDefinition]): Its text settings, set with GraphSetText.
            presets (list[GraphPresetState] | None | Unset): A compound's presets, which set its settings; empty for every
                other node.
     """

    path: str
    title: str
    category: str
    summary: str
    has_preview: bool
    ports: list[GraphPortDefinition]
    settings: list[GraphSettingDefinition]
    presets: list[GraphPresetState] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_port_definition import GraphPortDefinition # noqa: PLC0415
        from ..models.graph_preset_state import GraphPresetState # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        path = self.path

        title = self.title

        category = self.category

        summary = self.summary

        has_preview = self.has_preview

        ports = []
        for ports_item_data in self.ports:
            ports_item = ports_item_data.to_dict()
            ports.append(ports_item)



        settings = []
        for settings_item_data in self.settings:
            settings_item = settings_item_data.to_dict()
            settings.append(settings_item)



        presets: list[dict[str, Any]] | None | Unset
        if isinstance(self.presets, Unset):
            presets = UNSET
        elif isinstance(self.presets, list):
            presets = []
            for presets_type_1_item_data in self.presets:
                presets_type_1_item = presets_type_1_item_data.to_dict()
                presets.append(presets_type_1_item)


        else:
            presets = self.presets


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "path": path,
            "title": title,
            "category": category,
            "summary": summary,
            "hasPreview": has_preview,
            "ports": ports,
            "settings": settings,
        })
        if presets is not UNSET:
            field_dict["presets"] = presets

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_port_definition import GraphPortDefinition # noqa: PLC0415
        from ..models.graph_preset_state import GraphPresetState # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        d = dict(src_dict)
        path = d.pop("path")

        title = d.pop("title")

        category = d.pop("category")

        summary = d.pop("summary")

        has_preview = d.pop("hasPreview")

        ports = []
        _ports = d.pop("ports")
        for ports_item_data in (_ports):
            ports_item = GraphPortDefinition.from_dict(ports_item_data)



            ports.append(ports_item)


        settings = []
        _settings = d.pop("settings")
        for settings_item_data in (_settings):
            settings_item = GraphSettingDefinition.from_dict(settings_item_data)



            settings.append(settings_item)


        def _parse_presets(data: object) -> list[GraphPresetState] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                presets_type_1 = []
                _presets_type_1 = data
                for presets_type_1_item_data in (_presets_type_1):
                    presets_type_1_item = GraphPresetState.from_dict(presets_type_1_item_data)



                    presets_type_1.append(presets_type_1_item)

                return presets_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[GraphPresetState] | None | Unset, data)

        presets = _parse_presets(d.pop("presets", UNSET))


        graph_node_type = cls(
            path=path,
            title=title,
            category=category,
            summary=summary,
            has_preview=has_preview,
            ports=ports,
            settings=settings,
            presets=presets,
        )


        graph_node_type.additional_properties = d
        return graph_node_type

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
