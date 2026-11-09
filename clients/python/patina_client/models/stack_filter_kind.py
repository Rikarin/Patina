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
  from ..models.graph_setting_definition import GraphSettingDefinition
  from ..models.stack_filter_input import StackFilterInput





T = TypeVar("T", bound="StackFilterKind")



@_attrs_define
class StackFilterKind:
    """ An adjustment a filter layer or a mask can run, or a generator an input can read: any node or shipped compound
    a stack can use there. A project's own compounds reach a stack from the shelf.

        Attributes:
            node (str): The node type or compound, as string StackLayer.FilterNode names it.
            name (str): What a person calls it.
            category (str): The heading it sits under.
            summary (str): One line about it.
            ports (list[GraphPortDefinition]): Its numbers, each saying what it is and what it starts as. Never an image it
                reads.
            settings (list[GraphSettingDefinition]): What it takes as text.
            inputs (list[StackFilterInput] | None | Unset): The images it reads, its subject first. Empty for a generator.
     """

    node: str
    name: str
    category: str
    summary: str
    ports: list[GraphPortDefinition]
    settings: list[GraphSettingDefinition]
    inputs: list[StackFilterInput] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_port_definition import GraphPortDefinition # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        from ..models.stack_filter_input import StackFilterInput # noqa: PLC0415
        node = self.node

        name = self.name

        category = self.category

        summary = self.summary

        ports = []
        for ports_item_data in self.ports:
            ports_item = ports_item_data.to_dict()
            ports.append(ports_item)



        settings = []
        for settings_item_data in self.settings:
            settings_item = settings_item_data.to_dict()
            settings.append(settings_item)



        inputs: list[dict[str, Any]] | None | Unset
        if isinstance(self.inputs, Unset):
            inputs = UNSET
        elif isinstance(self.inputs, list):
            inputs = []
            for inputs_type_1_item_data in self.inputs:
                inputs_type_1_item = inputs_type_1_item_data.to_dict()
                inputs.append(inputs_type_1_item)


        else:
            inputs = self.inputs


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
            "name": name,
            "category": category,
            "summary": summary,
            "ports": ports,
            "settings": settings,
        })
        if inputs is not UNSET:
            field_dict["inputs"] = inputs

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_port_definition import GraphPortDefinition # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        from ..models.stack_filter_input import StackFilterInput # noqa: PLC0415
        d = dict(src_dict)
        node = d.pop("node")

        name = d.pop("name")

        category = d.pop("category")

        summary = d.pop("summary")

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


        def _parse_inputs(data: object) -> list[StackFilterInput] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                inputs_type_1 = []
                _inputs_type_1 = data
                for inputs_type_1_item_data in (_inputs_type_1):
                    inputs_type_1_item = StackFilterInput.from_dict(inputs_type_1_item_data)



                    inputs_type_1.append(inputs_type_1_item)

                return inputs_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackFilterInput] | None | Unset, data)

        inputs = _parse_inputs(d.pop("inputs", UNSET))


        stack_filter_kind = cls(
            node=node,
            name=name,
            category=category,
            summary=summary,
            ports=ports,
            settings=settings,
            inputs=inputs,
        )


        stack_filter_kind.additional_properties = d
        return stack_filter_kind

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
