from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.project_mesh_change import ProjectMeshChange
from typing import cast



def _get_kwargs(
    *,
    project: str,
    mesh: str,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["project"] = project

    params["mesh"] = mesh


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/project/mesh-change",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | ProjectMeshChange | None:
    if response.status_code == 200:
        response_200 = ProjectMeshChange.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | ProjectMeshChange]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    mesh: str,

) -> Response[ProblemDetails | ProjectMeshChange]:
    """ Whether a mesh file differs from the one the project already has, so that reloading has work to do.

    Args:
        project (str):
        mesh (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectMeshChange]
     """


    kwargs = _get_kwargs(
        project=project,
mesh=mesh,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    mesh: str,

) -> ProblemDetails | ProjectMeshChange | None:
    """ Whether a mesh file differs from the one the project already has, so that reloading has work to do.

    Args:
        project (str):
        mesh (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectMeshChange
     """


    return sync_detailed(
        client=client,
project=project,
mesh=mesh,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    mesh: str,

) -> Response[ProblemDetails | ProjectMeshChange]:
    """ Whether a mesh file differs from the one the project already has, so that reloading has work to do.

    Args:
        project (str):
        mesh (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectMeshChange]
     """


    kwargs = _get_kwargs(
        project=project,
mesh=mesh,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    mesh: str,

) -> ProblemDetails | ProjectMeshChange | None:
    """ Whether a mesh file differs from the one the project already has, so that reloading has work to do.

    Args:
        project (str):
        mesh (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectMeshChange
     """


    return (await asyncio_detailed(
        client=client,
project=project,
mesh=mesh,

    )).parsed
