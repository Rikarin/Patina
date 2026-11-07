from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_comment_state import GraphCommentState
  from ..models.graph_delta_settings import GraphDeltaSettings
  from ..models.graph_edge_state import GraphEdgeState
  from ..models.graph_group_state import GraphGroupState
  from ..models.graph_interface_port import GraphInterfacePort
  from ..models.graph_node_state import GraphNodeState
  from ..models.graph_setting_definition import GraphSettingDefinition





T = TypeVar("T", bound="GraphDelta")



@_attrs_define
class GraphDelta:
    """ What changed in the document, the same for an edit, an undo and a redo.

        Attributes:
            nodes (list[GraphNodeState]): Nodes that are new or differ, whole.
            removed_nodes (list[int]): Nodes that have gone.
            edges (list[GraphEdgeState]): Wires that are new.
            removed_edges (list[GraphEdgeState]): Wires that have gone.
            settings (GraphDeltaSettings): Graph settings that are new or differ.
            removed_settings (list[str]): Graph settings that have gone.
            parameters (list[GraphSettingDefinition] | None | Unset): The whole list of exposed parameters, or null when it
                did not change.
            groups (list[GraphGroupState] | None | Unset): The whole list of groups, or null when it did not change.
            comments (list[GraphCommentState] | None | Unset): The whole list of notes, or null when it did not change.
            interface (list[GraphInterfacePort] | None | Unset): The whole list of interface ports, or null when it did not
                change.
     """

    nodes: list[GraphNodeState]
    removed_nodes: list[int]
    edges: list[GraphEdgeState]
    removed_edges: list[GraphEdgeState]
    settings: GraphDeltaSettings
    removed_settings: list[str]
    parameters: list[GraphSettingDefinition] | None | Unset = UNSET
    groups: list[GraphGroupState] | None | Unset = UNSET
    comments: list[GraphCommentState] | None | Unset = UNSET
    interface: list[GraphInterfacePort] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_comment_state import GraphCommentState # noqa: PLC0415
        from ..models.graph_delta_settings import GraphDeltaSettings # noqa: PLC0415
        from ..models.graph_edge_state import GraphEdgeState # noqa: PLC0415
        from ..models.graph_group_state import GraphGroupState # noqa: PLC0415
        from ..models.graph_interface_port import GraphInterfacePort # noqa: PLC0415
        from ..models.graph_node_state import GraphNodeState # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        nodes = []
        for nodes_item_data in self.nodes:
            nodes_item = nodes_item_data.to_dict()
            nodes.append(nodes_item)



        removed_nodes = self.removed_nodes



        edges = []
        for edges_item_data in self.edges:
            edges_item = edges_item_data.to_dict()
            edges.append(edges_item)



        removed_edges = []
        for removed_edges_item_data in self.removed_edges:
            removed_edges_item = removed_edges_item_data.to_dict()
            removed_edges.append(removed_edges_item)



        settings = self.settings.to_dict()

        removed_settings = self.removed_settings



        parameters: list[dict[str, Any]] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, list):
            parameters = []
            for parameters_type_1_item_data in self.parameters:
                parameters_type_1_item = parameters_type_1_item_data.to_dict()
                parameters.append(parameters_type_1_item)


        else:
            parameters = self.parameters

        groups: list[dict[str, Any]] | None | Unset
        if isinstance(self.groups, Unset):
            groups = UNSET
        elif isinstance(self.groups, list):
            groups = []
            for groups_type_1_item_data in self.groups:
                groups_type_1_item = groups_type_1_item_data.to_dict()
                groups.append(groups_type_1_item)


        else:
            groups = self.groups

        comments: list[dict[str, Any]] | None | Unset
        if isinstance(self.comments, Unset):
            comments = UNSET
        elif isinstance(self.comments, list):
            comments = []
            for comments_type_1_item_data in self.comments:
                comments_type_1_item = comments_type_1_item_data.to_dict()
                comments.append(comments_type_1_item)


        else:
            comments = self.comments

        interface: list[dict[str, Any]] | None | Unset
        if isinstance(self.interface, Unset):
            interface = UNSET
        elif isinstance(self.interface, list):
            interface = []
            for interface_type_1_item_data in self.interface:
                interface_type_1_item = interface_type_1_item_data.to_dict()
                interface.append(interface_type_1_item)


        else:
            interface = self.interface


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "nodes": nodes,
            "removedNodes": removed_nodes,
            "edges": edges,
            "removedEdges": removed_edges,
            "settings": settings,
            "removedSettings": removed_settings,
        })
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if groups is not UNSET:
            field_dict["groups"] = groups
        if comments is not UNSET:
            field_dict["comments"] = comments
        if interface is not UNSET:
            field_dict["interface"] = interface

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_comment_state import GraphCommentState # noqa: PLC0415
        from ..models.graph_delta_settings import GraphDeltaSettings # noqa: PLC0415
        from ..models.graph_edge_state import GraphEdgeState # noqa: PLC0415
        from ..models.graph_group_state import GraphGroupState # noqa: PLC0415
        from ..models.graph_interface_port import GraphInterfacePort # noqa: PLC0415
        from ..models.graph_node_state import GraphNodeState # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        d = dict(src_dict)
        nodes = []
        _nodes = d.pop("nodes")
        for nodes_item_data in (_nodes):
            nodes_item = GraphNodeState.from_dict(nodes_item_data)



            nodes.append(nodes_item)


        removed_nodes = cast(list[int], d.pop("removedNodes"))


        edges = []
        _edges = d.pop("edges")
        for edges_item_data in (_edges):
            edges_item = GraphEdgeState.from_dict(edges_item_data)



            edges.append(edges_item)


        removed_edges = []
        _removed_edges = d.pop("removedEdges")
        for removed_edges_item_data in (_removed_edges):
            removed_edges_item = GraphEdgeState.from_dict(removed_edges_item_data)



            removed_edges.append(removed_edges_item)


        settings = GraphDeltaSettings.from_dict(d.pop("settings"))




        removed_settings = cast(list[str], d.pop("removedSettings"))


        def _parse_parameters(data: object) -> list[GraphSettingDefinition] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                parameters_type_1 = []
                _parameters_type_1 = data
                for parameters_type_1_item_data in (_parameters_type_1):
                    parameters_type_1_item = GraphSettingDefinition.from_dict(parameters_type_1_item_data)



                    parameters_type_1.append(parameters_type_1_item)

                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[GraphSettingDefinition] | None | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        def _parse_groups(data: object) -> list[GraphGroupState] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                groups_type_1 = []
                _groups_type_1 = data
                for groups_type_1_item_data in (_groups_type_1):
                    groups_type_1_item = GraphGroupState.from_dict(groups_type_1_item_data)



                    groups_type_1.append(groups_type_1_item)

                return groups_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[GraphGroupState] | None | Unset, data)

        groups = _parse_groups(d.pop("groups", UNSET))


        def _parse_comments(data: object) -> list[GraphCommentState] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                comments_type_1 = []
                _comments_type_1 = data
                for comments_type_1_item_data in (_comments_type_1):
                    comments_type_1_item = GraphCommentState.from_dict(comments_type_1_item_data)



                    comments_type_1.append(comments_type_1_item)

                return comments_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[GraphCommentState] | None | Unset, data)

        comments = _parse_comments(d.pop("comments", UNSET))


        def _parse_interface(data: object) -> list[GraphInterfacePort] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                interface_type_1 = []
                _interface_type_1 = data
                for interface_type_1_item_data in (_interface_type_1):
                    interface_type_1_item = GraphInterfacePort.from_dict(interface_type_1_item_data)



                    interface_type_1.append(interface_type_1_item)

                return interface_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[GraphInterfacePort] | None | Unset, data)

        interface = _parse_interface(d.pop("interface", UNSET))


        graph_delta = cls(
            nodes=nodes,
            removed_nodes=removed_nodes,
            edges=edges,
            removed_edges=removed_edges,
            settings=settings,
            removed_settings=removed_settings,
            parameters=parameters,
            groups=groups,
            comments=comments,
            interface=interface,
        )


        graph_delta.additional_properties = d
        return graph_delta

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
